use tauri::{AppHandle, State};

use crate::managers::coordinator::CoordinatorState;
use crate::state::AppState;

#[tauri::command]
#[specta::specta]
pub fn toggle_recording(
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<CoordinatorState, String> {
    let settings = state.settings.lock().map_err(|e| e.to_string())?.clone();

    let mut coordinator = state.coordinator.lock().map_err(|e| e.to_string())?;
    let new_state = coordinator.toggle(
        &app,
        settings.selected_microphone.as_deref(),
        &settings.language,
        settings.enable_vad,
        settings.vad_threshold as f32,
        settings.auto_paste,
    );

    Ok(new_state)
}

#[tauri::command]
#[specta::specta]
pub fn cancel_recording(
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<(), String> {
    let mut coordinator = state.coordinator.lock().map_err(|e| e.to_string())?;
    coordinator.cancel(&app);
    Ok(())
}
