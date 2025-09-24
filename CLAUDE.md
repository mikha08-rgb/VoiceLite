# VoiceLite - Native Windows Speech-to-Text

## Project Overview
I'm building a Windows native speech-to-text application that uses Whisper AI for highly accurate transcription. The app runs in the background and allows voice dictation in ANY Windows application via global hotkey.

## Core Requirements
- Platform: Windows-only native application
- Technology: C# with WPF (.NET 6.0 or later)
- Speech Recognition: whisper.cpp with ggml-small.bin model (466MB)
- Global Hotkey: F1 (customizable later)
- Text Injection: Works everywhere - terminals, browsers, IDEs, any text field
- System Tray: Background operation with status indicator
- Performance: <200ms latency, 95%+ accuracy on technical terms

## Technical Stack Decided
- Language: C# (NOT Electron, NOT Python)
- UI Framework: WPF
- Audio Recording: NAudio NuGet package
- Whisper Integration: whisper.cpp subprocess (NOT Web Speech API)
- Text Injection: InputSimulator NuGet package
- Global Hotkeys: Win32 API RegisterHotKey

## Project Structure
VoiceLite/
├── VoiceLite.sln
├── VoiceLite/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── Services/
│   │   ├── AudioRecorder.cs       # NAudio recording implementation
│   │   ├── WhisperService.cs      # Manages whisper.cpp subprocess
│   │   ├── TextInjector.cs        # InputSimulator wrapper
│   │   ├── HotkeyManager.cs       # Global hotkey registration
│   │   └── SystemTrayManager.cs   # System tray icon and menu
│   ├── Models/
│   │   ├── Settings.cs            # User settings (hotkey, mode, etc)
│   │   └── TranscriptionResult.cs # Result from Whisper
│   └── Interfaces/
│       ├── IRecorder.cs
│       ├── ITranscriber.cs
│       └── ITextInjector.cs
├── whisper/
│   ├── whisper.exe               # whisper.cpp Windows binary
│   └── ggml-small.bin           # Whisper AI model (466MB)
├── temp/
│   └── audio.wav                # Temporary recording file
└── settings.json                # User preferences
## Implementation Plan

### Phase 1: Core Functionality (MVP)
1. [ ] Create WPF project with basic window
2. [ ] Add NAudio and implement AudioRecorder service
3. [ ] Test recording to WAV file
4. [ ] Integrate whisper.cpp as subprocess
5. [ ] Parse transcription output
6. [ ] Add InputSimulator for text injection
7. [ ] Implement global F1 hotkey
8. [ ] Add system tray icon

### Phase 2: Essential Features
9. [ ] Add settings window
10. [ ] Implement push-to-talk vs toggle mode
11. [ ] Add customizable hotkey selection
12. [ ] Error handling (no mic, whisper missing)
13. [ ] Visual/audio recording indicator

### Phase 3: Future Enhancements
- [ ] Voice commands ("new line", "period")
- [ ] App-specific behavior
- [ ] Custom vocabulary
- [ ] Multiple language support

## Key Architecture Decisions

### Use Dependency Injection Pattern
```csharp
public interface IRecorder {
    void StartRecording();
    void StopRecording();
    event EventHandler<AudioDataArgs> AudioReady;
}

public interface ITranscriber {
    Task<string> TranscribeAsync(byte[] audio);
}

public interface ITextInjector {
    void InjectText(string text);
}

Service-Based Architecture
Keep services modular and swappable. Each service has single responsibility.
Settings Management

public class Settings {
    public RecordMode Mode { get; set; } = RecordMode.Toggle;
    public Keys RecordHotkey { get; set; } = Keys.F1;
    public string WhisperModel { get; set; } = "ggml-small.bin";
    public bool StartWithWindows { get; set; } = false;
    public bool ShowTrayIcon { get; set; } = true;
}

Whisper Integration Details
Whisper.cpp Command
whisper.exe -m ggml-small.bin -f audio.wav --no-timestamps --language en

Procces management 
var process = new Process {
    StartInfo = new ProcessStartInfo {
        FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe"),
        Arguments = $"-m ggml-small.bin -f \"{audioPath}\" --no-timestamps",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        CreateNoWindow = true
    }
};
NuGet Packages Required
    •    NAudio - Audio recording
    •    InputSimulator - Text injection
    •    System.Text.Json - Settings serialization
    •    Hardcodet.NotifyIcon.Wpf - System tray (optional, or use Windows.Forms)
Current Development Status
Starting fresh - no code written yet. Need to:
    1.    Create Visual Studio WPF project
    2.    Set up folder structure
    3.    Install NuGet packages
    4.    Build MVP
Important Notes for Claude Code
    •    I have Visual Studio 2022 installed (NOT just VS Code)
    •    I want production-quality code, not prototypes
    •    Include error handling from the start
    •    Make services modular for easy future changes
    •    Performance is critical - optimize for <200ms latency
    •    The app should feel native and professional
Common Issues to Avoid
    1.    Whisper.exe path - use relative to exe location
    2.    Audio format - must be 16kHz WAV for Whisper
    3.    Admin rights - may need for some text injection scenarios
    4.    Antivirus - may flag InputSimulator or global hotkeys
Testing Checklist
    •    Works in Notepad
    •    Works in Terminal/CMD
    •    Works in browsers
    •    Works in VS Code
    •    Works with admin applications
    •    Handles no microphone gracefully
    •    Handles missing whisper.exe gracefully
Success Metrics
    •    Transcription latency: <200ms after speech stops
    •    Accuracy on code terms: 95%+ (git, npm, useState, forEach, etc)
    •    App size: <20MB (excluding Whisper model)
    •    RAM usage: <100MB when idle, <300MB when transcribing
    •    CPU usage: <5% when idle