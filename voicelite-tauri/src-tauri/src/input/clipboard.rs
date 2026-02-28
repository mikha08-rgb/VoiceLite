use std::thread;
use std::time::Duration;

use anyhow::{Context, Result};
use arboard::Clipboard;
use enigo::{Direction, Enigo, Key, Keyboard, Settings};

/// Sets clipboard text and simulates Ctrl+V paste.
/// Timing ported from TextInjector.cs.
pub fn paste_text(text: &str) -> Result<()> {
    // Set clipboard
    let mut clipboard = Clipboard::new().context("Failed to access clipboard")?;
    clipboard
        .set_text(text)
        .context("Failed to set clipboard text")?;

    // Small delay for clipboard to settle (20ms, ported from TextInjector.cs)
    thread::sleep(Duration::from_millis(20));

    // Simulate Ctrl+V
    let mut enigo = Enigo::new(&Settings::default())
        .map_err(|e| anyhow::anyhow!("Failed to create enigo: {:?}", e))?;

    enigo.key(Key::Control, Direction::Press)
        .map_err(|e| anyhow::anyhow!("Failed to press Ctrl: {:?}", e))?;
    enigo.key(Key::Unicode('v'), Direction::Click)
        .map_err(|e| anyhow::anyhow!("Failed to click V: {:?}", e))?;
    enigo.key(Key::Control, Direction::Release)
        .map_err(|e| anyhow::anyhow!("Failed to release Ctrl: {:?}", e))?;

    log::info!("Pasted {} chars via Ctrl+V", text.len());
    Ok(())
}
