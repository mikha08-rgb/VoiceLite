# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
VoiceLite is a production-ready Windows native speech-to-text application using OpenAI Whisper AI. It provides instant voice typing anywhere in Windows via global hotkey. The app is fully functional with comprehensive error handling, multiple Whisper models, and performance optimizations.

## Common Development Commands

### Build & Run
```bash
# Build the solution
dotnet build VoiceLite/VoiceLite.sln

# Run in Debug mode
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Build Release version
dotnet build VoiceLite/VoiceLite.sln -c Release

# Publish self-contained executable
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### Testing
```bash
# Run all tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Run specific test
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --filter "FullyQualifiedName~TestName"

# Run tests with coverage
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect:"XPlat Code Coverage" --settings VoiceLite/VoiceLite.Tests/coverlet.runsettings
```

### Code Quality
```bash
# Format code
dotnet format VoiceLite/VoiceLite.sln

# Analyze code (if analyzers are installed)
dotnet build VoiceLite/VoiceLite.sln /p:RunAnalyzers=true
```

## Architecture Overview

### Core Components

1. **MainWindow** (`MainWindow.xaml.cs`): Entry point, coordinates all services
   - Manages recording state and hotkey handling
   - Implements push-to-talk and toggle modes
   - Handles system tray integration
   - Visual state management for recording indicators

2. **Service Layer** (`Services/`): Modular, single-responsibility services
   - `AudioRecorder`: NAudio-based recording with noise suppression
   - `WhisperService`/`PersistentWhisperService`: Whisper.cpp subprocess management
   - `TextInjector`: Text injection using InputSimulator (supports multiple modes)
   - `HotkeyManager`: Global hotkey registration via Win32 API
   - `SystemTrayManager`: System tray icon and context menu
   - `AudioPreprocessor`: Audio enhancement (noise gate, gain control)
   - `TranscriptionPostProcessor`: Text corrections and formatting
   - `WhisperProcessPool`: Process pooling for performance
   - `ModelBenchmarkService`: Model performance testing
   - `MemoryMonitor`: Memory usage tracking
   - `ErrorLogger`: Centralized error logging

3. **Models** (`Models/`): Data structures and configuration
   - `Settings`: User preferences with validation
   - `TranscriptionResult`: Whisper output parsing
   - `WhisperModelInfo`: Model metadata and benchmarking

4. **Interfaces** (`Interfaces/`): Contract definitions for dependency injection
   - `IRecorder`, `ITranscriber`, `ITextInjector`

5. **UI Components**
   - `SettingsWindow`: Settings UI with model selection
   - `Controls/SimpleModelSelector`: Model selection control
   - `Resources/ModernStyles.xaml`: WPF styling resources

## Whisper Integration

### Available Models (in `whisper/` directory)
- `ggml-tiny.bin` (77MB): Fastest, lowest accuracy
- `ggml-base.bin` (148MB): Fast, good for basic use
- `ggml-small.bin` (488MB): **Default** - balanced performance
- `ggml-medium.bin` (1.5GB): Higher accuracy, slower
- `ggml-large-v3.bin` (3.1GB): Best accuracy, slowest

### Whisper Process Management
- Uses process pooling for performance (`WhisperProcessPool`)
- Persistent mode available for reduced latency
- Automatic timeout handling with configurable multiplier
- Temperature optimization (0.2) for better accuracy
- Context prompting using recent transcriptions
- Beam search parameters (beam_size=5, best_of=5)

### Whisper Command Format
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en --temperature 0.2 --beam-size 5 --best-of 5
```

## Key Technical Details

### Dependencies (NuGet Packages)
- `NAudio` (2.2.1): Audio recording and processing
- `H.InputSimulator` (1.2.1): Keyboard/mouse simulation for text injection
- `Hardcodet.NotifyIcon.Wpf` (2.0.1): System tray integration
- `System.Text.Json` (9.0.9): Settings persistence
- `System.Management` (8.0.0): System information

### Test Dependencies (xUnit)
- `xunit` (2.9.2): Test framework
- `Moq` (4.20.70): Mocking framework
- `FluentAssertions` (6.12.0): Assertion library

### Performance Optimizations
- Audio preprocessing with noise gate and AGC
- Whisper process pooling for reduced latency
- Smart text injection (clipboard for long text, typing for short)
- Memory monitoring and cleanup
- Cached model benchmarking
- VAD (Voice Activity Detection) for silence trimming

### Settings & Configuration
- Settings stored in `%APPDATA%/VoiceLite/settings.json`
- Default hotkey: Left Alt (customizable)
- Default mode: Push-to-talk (not toggle)
- Auto-paste enabled by default
- Multiple text injection modes (SmartAuto, AlwaysType, AlwaysPaste, PreferType, PreferPaste)
- Sound feedback disabled by default

### Error Handling & Logging
- Comprehensive error logging via `ErrorLogger` service
- Logs stored in `%APPDATA%/VoiceLite/logs/`
- Graceful fallbacks for missing models or whisper.exe
- Microphone device detection and switching
- Process crash recovery
- Timeout handling for hung processes

## Important Development Notes

### Platform Requirements
- Target Framework: .NET 8.0 Windows
- Requires Visual C++ Runtime 2015-2022 x64
- Windows 10/11 (uses Windows Forms for some features)

### Build Configuration
- Application manifest for DPI awareness (`app.manifest`)
- Icon resources embedded in project (`VoiceLite.ico`)
- Whisper binaries copied to output on build
- Self-contained deployment supported

### Critical Implementation Details
1. **Audio Format**: Must be 16kHz, 16-bit mono WAV for Whisper
2. **Hotkey Registration**: Uses Win32 API, may require admin for some keys
3. **Text Injection**: May trigger antivirus warnings (false positives)
4. **Process Management**: Whisper.exe path relative to executable location
5. **Memory Management**: Automatic cleanup after transcription
6. **Thread Safety**: Recording state protected by lock

### Testing Compatibility
Works across all Windows applications:
- Text editors (Notepad, VS Code, Visual Studio)
- Terminals (CMD, PowerShell, Windows Terminal)
- Browsers (Chrome, Firefox, Edge)
- Communication apps (Discord, Teams, Slack)
- Games (windowed mode)
- Admin-elevated applications

### Performance Targets
- Transcription latency: <200ms after speech stops
- Accuracy on technical terms: 95%+ (git, npm, useState, forEach)
- Idle RAM usage: <100MB
- Active RAM usage: <300MB
- Idle CPU usage: <5%