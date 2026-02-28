use serde::{Deserialize, Serialize};
use tauri::AppHandle;
use tauri_plugin_store::StoreExt;

const STORE_PATH: &str = "settings.json";
const STORE_KEY: &str = "app_settings";

#[derive(Debug, Clone, Serialize, Deserialize, specta::Type, PartialEq)]
pub enum RecordMode {
    Toggle,
    Hold,
}

#[derive(Debug, Clone, Serialize, Deserialize, specta::Type)]
pub struct AppSettings {
    pub record_mode: RecordMode,
    pub hotkey: String,
    pub selected_microphone: Option<String>,
    pub whisper_model: String,
    pub language: String,
    pub enable_vad: bool,
    pub vad_threshold: f64,
    pub auto_paste: bool,
    pub show_tray_icon: bool,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            record_mode: RecordMode::Hold,
            hotkey: "Ctrl+Space".to_string(),
            selected_microphone: None,
            whisper_model: "ggml-base.bin".to_string(),
            language: "en".to_string(),
            enable_vad: true,
            vad_threshold: 0.35,
            auto_paste: true,
            show_tray_icon: true,
        }
    }
}

pub fn load_settings(app: &AppHandle) -> AppSettings {
    let store = app.store(STORE_PATH).ok();
    store
        .and_then(|s| s.get(STORE_KEY))
        .and_then(|v| serde_json::from_value(v).ok())
        .unwrap_or_default()
}

pub fn save_settings(app: &AppHandle, settings: &AppSettings) -> Result<(), String> {
    let store = app.store(STORE_PATH).map_err(|e| e.to_string())?;
    let value = serde_json::to_value(settings).map_err(|e| e.to_string())?;
    store.set(STORE_KEY, value);
    store.save().map_err(|e| e.to_string())?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_settings() {
        let settings = AppSettings::default();
        assert_eq!(settings.hotkey, "Ctrl+Space");
        assert_eq!(settings.whisper_model, "ggml-base.bin");
        assert_eq!(settings.language, "en");
        assert!(settings.enable_vad);
        assert!((settings.vad_threshold - 0.35).abs() < f64::EPSILON);
        assert!(settings.auto_paste);
        assert!(settings.show_tray_icon);
        assert_eq!(settings.record_mode, RecordMode::Hold);
        assert_eq!(settings.selected_microphone, None);
    }

    #[test]
    fn test_settings_roundtrip_serde() {
        let settings = AppSettings::default();
        let json = serde_json::to_string(&settings).unwrap();
        let deserialized: AppSettings = serde_json::from_str(&json).unwrap();
        assert_eq!(deserialized.hotkey, settings.hotkey);
        assert_eq!(deserialized.whisper_model, settings.whisper_model);
        assert_eq!(deserialized.language, settings.language);
        assert_eq!(deserialized.enable_vad, settings.enable_vad);
        assert_eq!(deserialized.auto_paste, settings.auto_paste);
    }

    #[test]
    fn test_settings_custom_values_roundtrip() {
        let mut settings = AppSettings::default();
        settings.hotkey = "Alt+R".to_string();
        settings.selected_microphone = Some("My Mic".to_string());
        settings.whisper_model = "ggml-small.bin".to_string();
        settings.language = "es".to_string();
        settings.enable_vad = false;
        settings.vad_threshold = 0.5;
        settings.auto_paste = false;

        let json = serde_json::to_string(&settings).unwrap();
        let deserialized: AppSettings = serde_json::from_str(&json).unwrap();
        assert_eq!(deserialized.hotkey, "Alt+R");
        assert_eq!(deserialized.selected_microphone, Some("My Mic".to_string()));
        assert_eq!(deserialized.whisper_model, "ggml-small.bin");
        assert_eq!(deserialized.language, "es");
        assert!(!deserialized.enable_vad);
        assert!((deserialized.vad_threshold - 0.5).abs() < f64::EPSILON);
        assert!(!deserialized.auto_paste);
    }
}
