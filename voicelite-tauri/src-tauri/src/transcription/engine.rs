use std::path::PathBuf;
use std::panic;

use anyhow::{Context, Result};

pub struct WhisperEngine {
    ctx: Option<whisper_rs::WhisperContext>,
    model_path: PathBuf,
}

impl WhisperEngine {
    pub fn new(model_path: PathBuf) -> Self {
        Self {
            ctx: None,
            model_path,
        }
    }

    /// Loads the model into memory. Call from a background thread.
    pub fn load(&mut self) -> Result<()> {
        if self.ctx.is_some() {
            return Ok(());
        }

        log::info!("Loading whisper model: {:?}", self.model_path);

        let ctx = whisper_rs::WhisperContext::new_with_params(
            self.model_path.to_str().context("Invalid model path")?,
            whisper_rs::WhisperContextParameters::default(),
        )
        .map_err(|e| anyhow::anyhow!("Failed to load whisper model: {}", e))?;

        self.ctx = Some(ctx);
        log::info!("Whisper model loaded successfully");
        Ok(())
    }

    /// Transcribes audio samples (16kHz mono f32) to text.
    /// Uses catch_unwind for panic safety since whisper.cpp is C++.
    pub fn transcribe(&mut self, samples: &[f32], language: &str) -> Result<String> {
        if self.ctx.is_none() {
            self.load()?;
        }

        let ctx = self.ctx.as_ref().context("Whisper context not loaded")?;

        // catch_unwind around whisper-rs calls for panic safety
        let result = panic::catch_unwind(panic::AssertUnwindSafe(|| {
            Self::run_transcription(ctx, samples, language)
        }));

        match result {
            Ok(Ok(text)) => Ok(text),
            Ok(Err(e)) => Err(e),
            Err(_) => {
                log::error!("Whisper panicked during transcription — reloading engine");
                self.ctx = None;
                Err(anyhow::anyhow!(
                    "Transcription failed: whisper engine panicked"
                ))
            }
        }
    }

    fn run_transcription(
        ctx: &whisper_rs::WhisperContext,
        samples: &[f32],
        language: &str,
    ) -> Result<String> {
        let mut params = whisper_rs::FullParams::new(whisper_rs::SamplingStrategy::Greedy { best_of: 1 });

        params.set_language(Some(language));
        params.set_print_special(false);
        params.set_print_progress(false);
        params.set_print_realtime(false);
        params.set_print_timestamps(false);
        params.set_single_segment(true);
        params.set_no_context(true);
        params.set_suppress_blank(true);

        let mut state = ctx.create_state()
            .map_err(|e| anyhow::anyhow!("Failed to create whisper state: {}", e))?;

        state.full(params, samples)
            .map_err(|e| anyhow::anyhow!("Whisper transcription failed: {}", e))?;

        let num_segments = state.full_n_segments()
            .map_err(|e| anyhow::anyhow!("Failed to get segment count: {}", e))?;

        let mut text = String::new();
        for i in 0..num_segments {
            if let Ok(segment) = state.full_get_segment_text(i) {
                text.push_str(segment.trim());
                if i < num_segments - 1 {
                    text.push(' ');
                }
            }
        }

        Ok(text.trim().to_string())
    }

}
