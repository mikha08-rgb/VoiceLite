use tauri::State;

use crate::settings::{AppSettings, RecordMode};
use crate::state::AppState;

#[tauri::command]
#[specta::specta]
pub fn get_settings(state: State<'_, AppState>) -> Result<AppSettings, String> {
    Ok(state.settings.lock().map_err(|e| format!("Settings lock poisoned: {e}"))?.clone())
}

#[tauri::command]
#[specta::specta]
pub fn update_setting(
    state: State<'_, AppState>,
    key: String,
    value: String,
) -> Result<(), String> {
    {
        let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
        match key.as_str() {
            "record_mode" => {
                settings.record_mode = match value.as_str() {
                    "Toggle" => RecordMode::Toggle,
                    "Hold" => RecordMode::Hold,
                    _ => return Err(format!("Invalid record_mode: {}", value)),
                };
            }
            "hotkey" => settings.hotkey = value,
            "selected_microphone" => settings.selected_microphone = Some(value),
            "whisper_model" => {
                if !value.starts_with("ggml-") || !value.ends_with(".bin")
                    || value.contains('/') || value.contains('\\')
                {
                    return Err(format!("Invalid model filename: {}", value));
                }
                settings.whisper_model = value;
            }
            "language" => settings.language = value,
            "enable_vad" => {
                settings.enable_vad = value
                    .parse()
                    .map_err(|e: std::str::ParseBoolError| e.to_string())?
            }
            "vad_threshold" => {
                let threshold: f64 = value
                    .parse()
                    .map_err(|e: std::num::ParseFloatError| e.to_string())?;
                if !(0.0..=1.0).contains(&threshold) {
                    return Err("vad_threshold must be between 0.0 and 1.0".into());
                }
                settings.vad_threshold = threshold;
            }
            "auto_paste" => {
                settings.auto_paste = value
                    .parse()
                    .map_err(|e: std::str::ParseBoolError| e.to_string())?
            }
            "show_tray_icon" => {
                settings.show_tray_icon = value
                    .parse()
                    .map_err(|e: std::str::ParseBoolError| e.to_string())?
            }
            _ => return Err(format!("Unknown setting: {}", key)),
        }
    }
    state.save_settings()
}

#[tauri::command]
#[specta::specta]
pub fn reset_settings(state: State<'_, AppState>) -> Result<AppSettings, String> {
    {
        let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
        *settings = AppSettings::default();
    }
    state.save_settings()?;
    Ok(state.settings.lock().map_err(|e| format!("Settings lock poisoned: {e}"))?.clone())
}
