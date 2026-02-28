mod commands;
mod state;
mod settings;
mod tray;
mod managers;
mod audio;
mod transcription;
mod input;

use std::sync::Arc;

use tauri::Manager;
use tauri_specta::{collect_commands, Builder};
use specta_typescript::Typescript;

pub fn run() {
    let builder = Builder::<tauri::Wry>::new()
        .commands(collect_commands![
            commands::settings::get_settings,
            commands::settings::update_setting,
            commands::settings::reset_settings,
            commands::audio::get_audio_devices,
            commands::audio::set_audio_device,
            commands::transcription::toggle_recording,
            commands::transcription::cancel_recording,
        ]);

    #[cfg(debug_assertions)]
    builder
        .export(Typescript::default(), "../src/bindings.ts")
        .expect("Failed to export typescript bindings");

    tauri::Builder::default()
        .invoke_handler(builder.invoke_handler())
        .plugin(tauri_plugin_log::Builder::new().build())
        .plugin(tauri_plugin_store::Builder::new().build())
        .plugin(tauri_plugin_os::init())
        .plugin(tauri_plugin_clipboard_manager::init())
        .plugin(tauri_plugin_global_shortcut::Builder::new().build())
        .plugin(tauri_plugin_single_instance::init(|_app, _args, _cwd| {}))
        .setup(|app| {
            let app_state = state::AppState::new(app.handle())?;

            // Preload whisper model on background thread
            let tm = Arc::clone(&app_state.transcription_manager);
            std::thread::spawn(move || {
                if let Err(e) = tm.preload() {
                    log::error!("Failed to preload whisper model: {}", e);
                }
            });

            // Register global shortcut
            let handle = app.handle().clone();
            let settings = app_state.settings.lock()
                .map_err(|e| format!("Settings lock poisoned: {e}"))?;
            let hotkey = settings.hotkey.clone();
            let record_mode = settings.record_mode.clone();
            drop(settings);

            app.manage(app_state);

            // Register hotkey after managing state
            register_hotkey(&handle, &hotkey, &record_mode);

            Ok(())
        })
        .build(tauri::generate_context!())
        .expect("error while building tauri application")
        .run(|_app, _event| {});
}

fn register_hotkey(app: &tauri::AppHandle, hotkey: &str, record_mode: &settings::RecordMode) {
    use tauri_plugin_global_shortcut::{GlobalShortcutExt, ShortcutState};

    let handle = app.clone();
    let mode = record_mode.clone();
    let result = app.global_shortcut().on_shortcut(hotkey, move |_app, _shortcut, event| {
        let state = _app.state::<state::AppState>();
        let Ok(s) = state.settings.lock().map(|s| s.clone()) else {
            log::error!("Settings lock poisoned in hotkey handler");
            return;
        };
        let Ok(mut coordinator) = state.coordinator.lock() else {
            log::error!("Coordinator lock poisoned in hotkey handler");
            return;
        };

        match mode {
            settings::RecordMode::Toggle => {
                if event.state == ShortcutState::Pressed {
                    coordinator.toggle(
                        &handle,
                        s.selected_microphone.as_deref(),
                        &s.language,
                        s.enable_vad,
                        s.vad_threshold as f32,
                        s.auto_paste,
                    );
                }
            }
            settings::RecordMode::Hold => {
                match event.state {
                    ShortcutState::Pressed => {
                        coordinator.start(&handle, s.selected_microphone.as_deref());
                    }
                    ShortcutState::Released => {
                        coordinator.stop(
                            &handle,
                            &s.language,
                            s.enable_vad,
                            s.vad_threshold as f32,
                            s.auto_paste,
                        );
                    }
                }
            }
        }
    });

    match result {
        Ok(()) => log::info!("Registered global shortcut: {}", hotkey),
        Err(e) => log::error!("Failed to register shortcut '{}': {}", hotkey, e),
    }
}
