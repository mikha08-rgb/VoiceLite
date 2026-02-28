use std::sync::mpsc;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};

use anyhow::{Context, Result};
use cpal::traits::{DeviceTrait, HostTrait, StreamTrait};

const TARGET_SAMPLE_RATE: u32 = 16000;

pub struct AudioRecorder {
    stream: Option<cpal::Stream>,
    receiver: Option<mpsc::Receiver<Vec<f32>>>,
    stop_flag: Arc<AtomicBool>,
}

impl AudioRecorder {
    pub fn new() -> Self {
        Self {
            stream: None,
            receiver: None,
            stop_flag: Arc::new(AtomicBool::new(false)),
        }
    }

    pub fn start(&mut self, device_name: Option<&str>) -> Result<()> {
        let host = cpal::default_host();

        let device = match device_name {
            Some(name) => host
                .input_devices()
                .context("Failed to enumerate input devices")?
                .find(|d| d.name().ok().as_deref() == Some(name))
                .context(format!("Device not found: {}", name))?,
            None => host
                .default_input_device()
                .context("No default input device available")?,
        };

        let supported_configs = device
            .supported_input_configs()
            .context("Failed to get supported configs")?;

        // Try to find a config that supports 16kHz, otherwise use the closest
        let config = find_best_config(supported_configs, TARGET_SAMPLE_RATE)?;
        let actual_sample_rate = config.sample_rate().0;
        let channels = config.channels() as usize;

        let (sender, receiver) = mpsc::channel::<Vec<f32>>();
        self.stop_flag.store(false, Ordering::SeqCst);

        let err_fn = |err: cpal::StreamError| {
            log::error!("Audio stream error: {}", err);
        };

        let stream = device.build_input_stream(
            &config.into(),
            move |data: &[f32], _: &cpal::InputCallbackInfo| {
                // Convert multi-channel to mono by averaging
                let mono: Vec<f32> = if channels == 1 {
                    data.to_vec()
                } else {
                    data.chunks(channels)
                        .map(|frame| frame.iter().sum::<f32>() / channels as f32)
                        .collect()
                };

                // Simple linear interpolation if sample rate doesn't match
                let resampled = if actual_sample_rate != TARGET_SAMPLE_RATE {
                    resample_linear(&mono, actual_sample_rate, TARGET_SAMPLE_RATE)
                } else {
                    mono
                };

                let _ = sender.send(resampled);
            },
            err_fn,
            None,
        )
        .context("Failed to build input stream")?;

        stream.play().context("Failed to start audio stream")?;

        self.stream = Some(stream);
        self.receiver = Some(receiver);

        log::info!(
            "Recording started: {}Hz, {} channels (resampling to {}Hz mono)",
            actual_sample_rate,
            channels,
            TARGET_SAMPLE_RATE
        );

        Ok(())
    }

    pub fn stop(&mut self) -> Vec<f32> {
        self.stop_flag.store(true, Ordering::SeqCst);

        // Drop the stream to stop recording
        self.stream.take();

        // Drain all samples from the receiver
        let mut samples = Vec::new();
        if let Some(receiver) = self.receiver.take() {
            while let Ok(chunk) = receiver.try_recv() {
                samples.extend(chunk);
            }
        }

        log::info!("Recording stopped: {} samples collected", samples.len());
        samples
    }

    pub fn is_recording(&self) -> bool {
        self.stream.is_some()
    }
}

fn find_best_config(
    configs: cpal::SupportedInputConfigs,
    target_rate: u32,
) -> Result<cpal::SupportedStreamConfig> {
    let mut best: Option<cpal::SupportedStreamConfig> = None;
    let mut best_distance = u32::MAX;

    for config in configs {
        if config.sample_format() != cpal::SampleFormat::F32 {
            continue;
        }

        let min = config.min_sample_rate().0;
        let max = config.max_sample_rate().0;

        let rate = if target_rate >= min && target_rate <= max {
            target_rate
        } else if target_rate < min {
            min
        } else {
            max
        };

        let distance = (rate as i64 - target_rate as i64).unsigned_abs() as u32;
        if distance < best_distance {
            best_distance = distance;
            best = Some(config.with_sample_rate(cpal::SampleRate(rate)));
        }
    }

    // If no F32 format found, try any format
    if best.is_none() {
        let host = cpal::default_host();
        let device = host
            .default_input_device()
            .context("No default input device")?;
        let config = device
            .default_input_config()
            .context("No default input config")?;
        return Ok(config);
    }

    best.context("No suitable audio config found")
}

fn resample_linear(samples: &[f32], from_rate: u32, to_rate: u32) -> Vec<f32> {
    if from_rate == to_rate || samples.is_empty() {
        return samples.to_vec();
    }

    let ratio = from_rate as f64 / to_rate as f64;
    let output_len = (samples.len() as f64 / ratio).ceil() as usize;
    let mut output = Vec::with_capacity(output_len);

    for i in 0..output_len {
        let src_pos = i as f64 * ratio;
        let src_idx = src_pos as usize;
        let frac = src_pos - src_idx as f64;

        let sample = if src_idx + 1 < samples.len() {
            samples[src_idx] as f64 * (1.0 - frac) + samples[src_idx + 1] as f64 * frac
        } else if src_idx < samples.len() {
            samples[src_idx] as f64
        } else {
            0.0
        };

        output.push(sample as f32);
    }

    output
}
