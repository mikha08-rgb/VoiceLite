use std::sync::Arc;
use std::sync::mpsc;
use std::time::{Duration, Instant};

use tauri::{AppHandle, Emitter};

use crate::managers::audio_manager::AudioManager;
use crate::managers::transcription_manager::TranscriptionManager;
use crate::input::clipboard;

const DEBOUNCE_MS: u64 = 30;
const TRANSCRIPTION_TIMEOUT: Duration = Duration::from_secs(60);

#[derive(Debug, Clone, PartialEq, serde::Serialize, serde::Deserialize, specta::Type)]
pub enum CoordinatorState {
    Idle,
    Recording,
    Processing,
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize, specta::Type)]
pub struct StateEvent {
    pub state: CoordinatorState,
    pub text: Option<String>,
    pub error: Option<String>,
}

/// Signal sent from the background transcription thread when processing completes.
struct TranscriptionDone;

pub struct Coordinator {
    state: CoordinatorState,
    audio_manager: AudioManager,
    transcription_manager: Arc<TranscriptionManager>,
    last_toggle: Instant,
    recording_started: Option<Instant>,
    /// Receives a signal when background transcription completes.
    done_rx: Option<mpsc::Receiver<TranscriptionDone>>,
}

impl Coordinator {
    pub fn new(
        audio_manager: AudioManager,
        transcription_manager: Arc<TranscriptionManager>,
    ) -> Self {
        Self {
            state: CoordinatorState::Idle,
            audio_manager,
            transcription_manager,
            last_toggle: Instant::now() - Duration::from_secs(1), // allow immediate first toggle
            recording_started: None,
            done_rx: None,
        }
    }

    /// Sync state from background thread. Call before any state check.
    /// Drains the completion channel — if the background thread signaled done,
    /// transition back to Idle atomically.
    fn sync_state(&mut self) {
        if self.state == CoordinatorState::Processing {
            if let Some(rx) = &self.done_rx {
                if rx.try_recv().is_ok() {
                    self.state = CoordinatorState::Idle;
                    self.done_rx = None;
                }
            }
        }
    }

    /// Start recording (for hold mode — called on key press).
    pub fn start(
        &mut self,
        app: &AppHandle,
        device_name: Option<&str>,
    ) {
        self.sync_state();
        if self.state != CoordinatorState::Idle {
            return;
        }
        self.start_recording(app, device_name);
    }

    /// Stop recording and transcribe (for hold mode — called on key release).
    pub fn stop(
        &mut self,
        app: &AppHandle,
        language: &str,
        enable_vad: bool,
        vad_threshold: f32,
        auto_paste: bool,
    ) {
        if self.state != CoordinatorState::Recording {
            return;
        }
        self.stop_and_transcribe(app, language, enable_vad, vad_threshold, auto_paste);
    }

    /// Handle toggle (hotkey press). Returns the new state.
    pub fn toggle(
        &mut self,
        app: &AppHandle,
        device_name: Option<&str>,
        language: &str,
        enable_vad: bool,
        vad_threshold: f32,
        auto_paste: bool,
    ) -> CoordinatorState {
        self.sync_state();

        // Debounce: ignore toggles within 30ms
        let now = Instant::now();
        if now.duration_since(self.last_toggle) < Duration::from_millis(DEBOUNCE_MS) {
            return self.state.clone();
        }
        self.last_toggle = now;

        match self.state {
            CoordinatorState::Idle => {
                self.start_recording(app, device_name);
            }
            CoordinatorState::Recording => {
                self.stop_and_transcribe(app, language, enable_vad, vad_threshold, auto_paste);
            }
            CoordinatorState::Processing => {
                // Ignore toggle during processing
            }
        }

        self.state.clone()
    }

    pub fn cancel(&mut self, app: &AppHandle) {
        self.state = CoordinatorState::Idle;
        self.recording_started = None;
        self.done_rx = None;
        if self.audio_manager.is_recording() {
            let _ = self.audio_manager.stop_recording(false, 0.0);
        }
        emit_state(app, &self.state, None, None);
    }

    fn start_recording(&mut self, app: &AppHandle, device_name: Option<&str>) {
        match self.audio_manager.start_recording(device_name) {
            Ok(()) => {
                self.state = CoordinatorState::Recording;
                self.recording_started = Some(Instant::now());
                emit_state(app, &self.state, None, None);

                // Show overlay
                if let Some(overlay) = app.get_webview_window("overlay") {
                    let _ = overlay.show();
                }
            }
            Err(e) => {
                log::error!("Failed to start recording: {}", e);
                self.state = CoordinatorState::Idle;
                emit_state(app, &self.state, None, Some(e.to_string()));
            }
        }
    }

    fn stop_and_transcribe(
        &mut self,
        app: &AppHandle,
        language: &str,
        enable_vad: bool,
        vad_threshold: f32,
        auto_paste: bool,
    ) {
        self.state = CoordinatorState::Processing;
        self.recording_started = None;
        emit_state(app, &self.state, None, None);

        // Stop recording and get samples (with VAD if enabled)
        let samples = self.audio_manager.stop_recording(enable_vad, vad_threshold);

        if samples.is_empty() {
            log::warn!("No audio samples recorded");
            self.state = CoordinatorState::Idle;
            self.done_rx = None;
            emit_state(app, &self.state, None, Some("No audio recorded".into()));
            hide_overlay(app);
            return;
        }

        // Set up completion channel
        let (done_tx, done_rx) = mpsc::channel();
        self.done_rx = Some(done_rx);

        // Transcribe on a background thread to not block UI
        let transcription_manager = Arc::clone(&self.transcription_manager);
        let language = language.to_string();
        let app_handle = app.clone();

        std::thread::spawn(move || {
            // Run transcription with a timeout
            let (result_tx, result_rx) = mpsc::channel();
            let tm = Arc::clone(&transcription_manager);
            let lang = language.clone();

            let worker = std::thread::spawn(move || {
                let result = tm.transcribe(&samples, &lang);
                let _ = result_tx.send(result);
            });

            let transcription_result = match result_rx.recv_timeout(TRANSCRIPTION_TIMEOUT) {
                Ok(result) => result,
                Err(mpsc::RecvTimeoutError::Timeout) => {
                    log::error!("Transcription timed out after {:?}", TRANSCRIPTION_TIMEOUT);
                    // Worker thread will eventually finish — we just move on
                    drop(worker);
                    Err(anyhow::anyhow!("Transcription timed out"))
                }
                Err(mpsc::RecvTimeoutError::Disconnected) => {
                    log::error!("Transcription worker thread panicked");
                    Err(anyhow::anyhow!("Transcription worker crashed"))
                }
            };

            match transcription_result {
                Ok(text) => {
                    if text.is_empty() {
                        log::warn!("Transcription returned empty text");
                        emit_state(
                            &app_handle,
                            &CoordinatorState::Idle,
                            None,
                            Some("No speech detected".into()),
                        );
                    } else {
                        log::info!("Transcription: \"{}\"", text);

                        if auto_paste {
                            if let Err(e) = clipboard::paste_text(&text) {
                                log::error!("Failed to paste text: {}", e);
                            }
                        }

                        emit_state(
                            &app_handle,
                            &CoordinatorState::Idle,
                            Some(text),
                            None,
                        );
                    }
                }
                Err(e) => {
                    log::error!("Transcription failed: {}", e);
                    emit_state(
                        &app_handle,
                        &CoordinatorState::Idle,
                        None,
                        Some(e.to_string()),
                    );
                }
            }

            // Signal completion — coordinator will pick this up via sync_state()
            let _ = done_tx.send(TranscriptionDone);
            hide_overlay(&app_handle);
        });
    }
}

fn emit_state(app: &AppHandle, state: &CoordinatorState, text: Option<String>, error: Option<String>) {
    let event = StateEvent {
        state: state.clone(),
        text,
        error,
    };
    let _ = app.emit("state-changed", &event);
}

fn hide_overlay(app: &AppHandle) {
    if let Some(overlay) = app.get_webview_window("overlay") {
        let _ = overlay.hide();
    }
}

// We need Manager trait for get_webview_window
use tauri::Manager;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_constants() {
        assert_eq!(DEBOUNCE_MS, 30);
        assert_eq!(TRANSCRIPTION_TIMEOUT, Duration::from_secs(60));
    }
}
