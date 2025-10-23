# Cleanup Report

## Overview
- Baseline build verified with /mnt/c/Program Files/dotnet/dotnet.exe build VoiceLite/VoiceLite.sln (net8.0-windows).
- Addressed compilation break in HotkeyManager introduced during parallel dev work.
- Removed orphaned VoiceLite/VoiceLitebinDebugnet8.0-windowswhisper directory (no references in code/scripts).
- Centralised hotkey display formatting in new Utilities/HotkeyDisplayHelper shared by MainWindow and SettingsWindow.

## Project Inventory
- Entry Points: App.xaml.cs (startup), MainWindow.xaml.cs (primary UI), SettingsWindow.xaml.cs (settings modal).
- Core Services: Services/AudioRecorder, AudioPreprocessor, PersistentWhisperService, WhisperService, TextInjector, HotkeyManager, SystemTrayManager, MetricsTracker, TranscriptionPostProcessor.
- Interfaces & Models: Interfaces/IRecorder, ITextInjector, ITranscriber; Models/Settings (persisted user preferences).
- External Assets: whisper/ binaries and scripts (build-and-run.bat, test-whisper.ps1, download-whisper.ps1).

## Hotspots & Actions
- Hotkey Formatting Duplication: identical logic in MainWindow and SettingsWindow replaced with single helper (Utilities/HotkeyDisplayHelper.Format). Both call sites now share one implementation, reducing drift risk.
- HotkeyManager Signature Regression: fixed stray () in UnregisterCurrentHotkey to restore compilation (change mirrors intended API surface).
- Stale Build Output: removed empty VoiceLite/VoiceLitebinDebugnet8.0-windowswhisper folder after confirming no references.

## Metrics
- Build time steady (~1.4 s ➝ ~0.8 s after edits) with zero warnings/errors.
- Hotkey formatting logic now defined once instead of duplicated twice.
- File count +1 helper, −1 stale directory.

## Risks & Follow-ups
- HotkeyDisplayHelper is internal; external consumers cannot see it (consistent with existing assembly usage). If future modules need formatting, expose via dedicated utility namespace.
- MetricsTracker remains unused at runtime; consider pruning once feature flags confirm it is dormant.
- Continue running builds through Windows dotnet.exe path until a Linux SDK is installed.
