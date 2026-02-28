use std::path::PathBuf;

use anyhow::Result;

use crate::audio::recorder::AudioRecorder;
use crate::audio::vad::SileroVad;

pub struct AudioManager {
    recorder: AudioRecorder,
    vad: Option<SileroVad>,
}

impl AudioManager {
    pub fn new(vad_model_path: Option<PathBuf>) -> Self {
        let vad = vad_model_path.and_then(|path| {
            match SileroVad::new(&path) {
                Ok(v) => Some(v),
                Err(e) => {
                    log::error!("Failed to load VAD model: {}", e);
                    None
                }
            }
        });

        Self {
            recorder: AudioRecorder::new(),
            vad,
        }
    }

    pub fn start_recording(&mut self, device_name: Option<&str>) -> Result<()> {
        self.recorder.start(device_name)
    }

    pub fn stop_recording(&mut self, enable_vad: bool, vad_threshold: f32) -> Vec<f32> {
        let samples = self.recorder.stop();

        if enable_vad {
            if let Some(ref mut vad) = self.vad {
                let trimmed = vad.process(&samples, vad_threshold);
                log::info!(
                    "VAD: {} samples → {} samples (trimmed {}%)",
                    samples.len(),
                    trimmed.len(),
                    if samples.is_empty() {
                        0
                    } else {
                        100 - (trimmed.len() * 100 / samples.len())
                    }
                );
                return trimmed;
            }
        }

        samples
    }

    pub fn is_recording(&self) -> bool {
        self.recorder.is_recording()
    }
}
