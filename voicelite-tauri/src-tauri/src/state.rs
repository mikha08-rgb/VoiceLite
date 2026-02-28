use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use tauri::AppHandle;

use crate::managers::audio_manager::AudioManager;
use crate::managers::coordinator::Coordinator;
use crate::managers::transcription_manager::TranscriptionManager;
use crate::settings::{self, AppSettings};

/// Shared application state managed by Tauri.
///
/// **Lock ordering:** When acquiring multiple locks, always lock in this order
/// to prevent deadlock: `settings` -> `coordinator`. Never hold `settings`
/// while acquiring `coordinator` — clone settings and drop the lock first.
/// `transcription_manager` is `Arc`-wrapped and uses its own internal mutex.
pub struct AppState {
    pub settings: Mutex<AppSettings>,
    pub coordinator: Mutex<Coordinator>,
    pub transcription_manager: Arc<TranscriptionManager>,
    pub app_handle: AppHandle,
}

impl AppState {
    pub fn new(app: &AppHandle) -> Result<Self, Box<dyn std::error::Error>> {
        let settings = settings::load_settings(app);

        // Resolve model paths from bundled resources
        let resource_dir = app
            .path()
            .resource_dir()
            .unwrap_or_else(|_| PathBuf::from("."));

        let models_dir = resource_dir.join("resources").join("models");

        let vad_model_path = models_dir.join("silero_vad_v5.onnx");
        let vad_path = if vad_model_path.exists() {
            Some(vad_model_path)
        } else {
            log::warn!("VAD model not found at {:?}", vad_model_path);
            None
        };

        let whisper_model_path = models_dir.join(&settings.whisper_model);

        let audio_manager = AudioManager::new(vad_path);
        let transcription_manager = Arc::new(TranscriptionManager::new(whisper_model_path));
        let coordinator = Coordinator::new(audio_manager, Arc::clone(&transcription_manager));

        Ok(Self {
            settings: Mutex::new(settings),
            coordinator: Mutex::new(coordinator),
            transcription_manager,
            app_handle: app.clone(),
        })
    }

    pub fn save_settings(&self) -> Result<(), String> {
        let settings = self.settings.lock().map_err(|e| e.to_string())?;
        settings::save_settings(&self.app_handle, &settings)
    }
}

// Need PathResolver
use tauri::Manager;
