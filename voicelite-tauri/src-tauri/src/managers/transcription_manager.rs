use std::path::PathBuf;
use std::sync::Mutex;

use anyhow::Result;

use crate::transcription::engine::WhisperEngine;

pub struct TranscriptionManager {
    engine: Mutex<WhisperEngine>,
}

impl TranscriptionManager {
    pub fn new(model_path: PathBuf) -> Self {
        Self {
            engine: Mutex::new(WhisperEngine::new(model_path)),
        }
    }

    /// Pre-loads the model. Call from a background thread.
    pub fn preload(&self) -> Result<()> {
        let mut engine = self.engine.lock().map_err(|e| anyhow::anyhow!("{}", e))?;
        engine.load()
    }

    /// Transcribes audio samples to text. Thread-safe (mutex-protected).
    pub fn transcribe(&self, samples: &[f32], language: &str) -> Result<String> {
        let mut engine = self.engine.lock().map_err(|e| anyhow::anyhow!("{}", e))?;
        engine.transcribe(samples, language)
    }
}
