use tauri::State;

use crate::state::AppState;

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize, specta::Type)]
pub struct AudioDevice {
    pub name: String,
    pub is_default: bool,
}

#[tauri::command]
#[specta::specta]
pub fn get_audio_devices() -> Result<Vec<AudioDevice>, String> {
    use cpal::traits::{DeviceTrait, HostTrait};

    let host = cpal::default_host();
    let default_device_name = host
        .default_input_device()
        .and_then(|d| d.name().ok());

    let devices: Vec<AudioDevice> = host
        .input_devices()
        .map_err(|e| e.to_string())?
        .filter_map(|d| {
            let name = d.name().ok()?;
            let is_default = default_device_name.as_ref() == Some(&name);
            Some(AudioDevice { name, is_default })
        })
        .collect();

    Ok(devices)
}

#[tauri::command]
#[specta::specta]
pub fn set_audio_device(
    state: State<'_, AppState>,
    device_name: String,
) -> Result<(), String> {
    {
        let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
        settings.selected_microphone = Some(device_name);
    }
    state.save_settings()
}
