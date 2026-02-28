use std::path::Path;

use anyhow::{Context, Result};
use ort::value::Tensor;

const SAMPLE_RATE: i64 = 16000;
const WINDOW_SIZE: usize = 512;
const CONTEXT_SIZE: usize = 64;
const STATE_DIM: usize = 128;
const INPUT_SIZE: usize = WINDOW_SIZE + CONTEXT_SIZE; // 576

pub struct SileroVad {
    session: ort::session::Session,
}

impl SileroVad {
    pub fn new(model_path: &Path) -> Result<Self> {
        let session = ort::session::Session::builder()?
            .with_inter_threads(1)?
            .with_intra_threads(1)?
            .commit_from_file(model_path)
            .context("Failed to load Silero VAD ONNX model")?;

        log::info!("Silero VAD loaded from {:?}", model_path);
        Ok(Self { session })
    }

    /// Trims silence from audio samples using VAD.
    /// Returns trimmed samples, or original if no speech detected or audio too short.
    pub fn process(&mut self, samples: &[f32], threshold: f32) -> Vec<f32> {
        if samples.len() < WINDOW_SIZE {
            return samples.to_vec();
        }

        let probabilities = match self.run_inference(samples) {
            Ok(p) => p,
            Err(e) => {
                log::error!("VAD inference failed: {}", e);
                return samples.to_vec();
            }
        };

        let segments = detect_speech_segments(
            &probabilities,
            threshold,
            200.0,  // speechPadMs
            500.0,  // minSilenceMs
            samples.len(),
        );

        if segments.is_empty() {
            return samples.to_vec();
        }

        concatenate_segments(samples, &segments)
    }

    fn run_inference(&mut self, samples: &[f32]) -> Result<Vec<f32>> {
        let total_windows = (samples.len().saturating_sub(CONTEXT_SIZE)) / WINDOW_SIZE;
        if total_windows == 0 {
            return Ok(vec![]);
        }

        let mut probabilities = Vec::with_capacity(total_windows);

        // State: [2, 1, 128] - initialized to zeros
        let mut state_data = vec![0.0f32; 2 * 1 * STATE_DIM];
        // Sample rate: [1]
        let sr_data = vec![SAMPLE_RATE];
        // Context buffer
        let mut context = vec![0.0f32; CONTEXT_SIZE];

        for w in 0..total_windows {
            let window_start = w * WINDOW_SIZE;

            // Build input: [1, 576]
            let mut input_data = vec![0.0f32; INPUT_SIZE];

            // Copy context (first 64 samples)
            input_data[..CONTEXT_SIZE].copy_from_slice(&context);

            // Copy window samples (next 512 samples)
            for i in 0..WINDOW_SIZE {
                let sample_idx = window_start + i;
                input_data[CONTEXT_SIZE + i] = if sample_idx < samples.len() {
                    samples[sample_idx]
                } else {
                    0.0
                };
            }

            // Update context for next iteration (last 64 samples of current window)
            let context_start = window_start + WINDOW_SIZE - CONTEXT_SIZE;
            for i in 0..CONTEXT_SIZE {
                let sample_idx = context_start + i;
                context[i] = if sample_idx < samples.len() {
                    samples[sample_idx]
                } else {
                    0.0
                };
            }

            let input_tensor = Tensor::from_array(
                ([1usize, INPUT_SIZE], input_data.into_boxed_slice())
            )?;
            let state_tensor = Tensor::from_array(
                ([2usize, 1, STATE_DIM], state_data.clone().into_boxed_slice())
            )?;
            let sr_tensor = Tensor::from_array(
                ([1usize], sr_data.clone().into_boxed_slice())
            )?;

            let outputs = self.session.run(ort::inputs![
                "input" => input_tensor,
                "state" => state_tensor,
                "sr" => sr_tensor,
            ])?;

            // Extract speech probability
            let (_shape, output_data) = outputs["output"]
                .try_extract_tensor::<f32>()
                .context("Failed to extract output tensor")?;
            probabilities.push(output_data[0]);

            // Update state for next window
            let (_shape, new_state_data) = outputs["stateN"]
                .try_extract_tensor::<f32>()
                .context("Failed to extract stateN tensor")?;
            state_data = new_state_data.to_vec();
        }

        Ok(probabilities)
    }
}

fn detect_speech_segments(
    probabilities: &[f32],
    threshold: f32,
    speech_pad_ms: f32,
    min_silence_ms: f32,
    total_samples: usize,
) -> Vec<(usize, usize)> {
    let pad_samples = (speech_pad_ms / 1000.0 * SAMPLE_RATE as f32) as usize;
    let min_silence_samples = (min_silence_ms / 1000.0 * SAMPLE_RATE as f32) as usize;

    // Find raw speech segments
    let mut raw_segments = Vec::new();
    let mut seg_start: Option<usize> = None;

    for (w, &prob) in probabilities.iter().enumerate() {
        let sample_pos = w * WINDOW_SIZE;

        if prob >= threshold {
            if seg_start.is_none() {
                seg_start = Some(sample_pos);
            }
        } else if let Some(start) = seg_start {
            raw_segments.push((start, sample_pos));
            seg_start = None;
        }
    }

    // Close any open segment
    if let Some(start) = seg_start {
        raw_segments.push((start, total_samples));
    }

    if raw_segments.is_empty() {
        return raw_segments;
    }

    // Merge segments separated by less than min_silence_ms
    let mut merged = vec![raw_segments[0]];
    for seg in raw_segments.iter().skip(1) {
        let prev = merged.last_mut().unwrap();
        if seg.0 - prev.1 < min_silence_samples {
            prev.1 = seg.1;
        } else {
            merged.push(*seg);
        }
    }

    // Add padding and clamp to bounds
    for seg in merged.iter_mut() {
        seg.0 = seg.0.saturating_sub(pad_samples);
        seg.1 = (seg.1 + pad_samples).min(total_samples);
    }

    merged
}

fn concatenate_segments(samples: &[f32], segments: &[(usize, usize)]) -> Vec<f32> {
    // 50ms silence gap between segments
    let gap_samples = (SAMPLE_RATE as usize) * 50 / 1000; // 800 samples at 16kHz

    let total_len: usize = segments.iter().map(|(s, e)| e - s).sum::<usize>()
        + (segments.len().saturating_sub(1)) * gap_samples;

    let mut result = vec![0.0f32; total_len];
    let mut pos = 0;

    for (i, &(start, end)) in segments.iter().enumerate() {
        let seg_len = end - start;
        result[pos..pos + seg_len].copy_from_slice(&samples[start..end]);
        pos += seg_len;

        if i < segments.len() - 1 {
            pos += gap_samples;
        }
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_detect_speech_segments_empty() {
        let result = detect_speech_segments(&[], 0.35, 200.0, 500.0, 0);
        assert!(result.is_empty());
    }

    #[test]
    fn test_detect_speech_segments_all_silence() {
        let probs = vec![0.1, 0.05, 0.2, 0.1];
        let result = detect_speech_segments(&probs, 0.35, 200.0, 500.0, 2048);
        assert!(result.is_empty());
    }

    #[test]
    fn test_detect_speech_segments_all_speech() {
        let probs = vec![0.9, 0.8, 0.85, 0.7];
        let total = 4 * WINDOW_SIZE;
        let result = detect_speech_segments(&probs, 0.35, 200.0, 500.0, total);
        assert_eq!(result.len(), 1);
        assert_eq!(result[0].0, 0);
        assert_eq!(result[0].1, total);
    }

    #[test]
    fn test_concatenate_segments() {
        let samples: Vec<f32> = (0..16000).map(|i| i as f32 / 16000.0).collect();
        let segments = vec![(0, 1000), (5000, 6000)];
        let result = concatenate_segments(&samples, &segments);
        // 1000 + 800 gap + 1000 = 2800
        assert_eq!(result.len(), 2800);
    }
}
