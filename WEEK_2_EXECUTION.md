# Week 2 Execution Plan: MVVM + DI

## Phase 1: Interfaces (Non-Breaking)
**Goal**: Define contracts without changing existing code

### Step 1.1: Create Interface Structure
```
VoiceLite/Core/Interfaces/
├── Services/
│   ├── IAudioRecorder.cs
│   ├── IWhisperService.cs
│   ├── ITextInjector.cs
│   ├── IHotkeyManager.cs
│   ├── ISystemTrayManager.cs
│   └── IErrorLogger.cs
├── Features/
│   ├── ILicenseService.cs
│   ├── IProFeatureService.cs
│   ├── ITranscriptionHistoryService.cs
│   └── ISettingsService.cs
└── Controllers/
    ├── IRecordingController.cs
    └── ITranscriptionController.cs
```

### Step 1.2: Interface Definitions
```csharp
// IAudioRecorder.cs
namespace VoiceLite.Core.Interfaces.Services
{
    public interface IAudioRecorder : IDisposable
    {
        bool IsRecording { get; }
        event EventHandler<string> AudioFileReady;
        event EventHandler<Exception> RecordingError;
        void StartRecording();
        void StopRecording();
        Task<string> GetLastAudioFileAsync();
    }
}

// IWhisperService.cs
namespace VoiceLite.Core.Interfaces.Services
{
    public interface IWhisperService : IDisposable
    {
        bool IsProcessing { get; }
        event EventHandler<string> TranscriptionComplete;
        event EventHandler<Exception> TranscriptionError;
        Task<string> TranscribeAsync(string audioFilePath, string modelPath);
        void CancelTranscription();
    }
}

// ITextInjector.cs
namespace VoiceLite.Core.Interfaces.Services
{
    public interface ITextInjector
    {
        enum InjectionMode { Type, Paste, SmartAuto }
        Task InjectTextAsync(string text, InjectionMode mode);
        bool CanInject();
    }
}
```

## Phase 2: Service Adaptation (Minimal Changes)
**Goal**: Make existing services implement interfaces

### Step 2.1: Update Service Classes
```csharp
// AudioRecorder.cs
public class AudioRecorder : IAudioRecorder  // Just add interface
{
    // Existing code unchanged
}

// PersistentWhisperService.cs
public class PersistentWhisperService : IWhisperService
{
    // Existing code unchanged
}
```

### Step 2.2: Create Service Factory (Temporary)
```csharp
// ServiceFactory.cs - Bridge between old and new
public static class ServiceFactory
{
    public static IAudioRecorder CreateAudioRecorder()
        => new AudioRecorder();

    public static IWhisperService CreateWhisperService()
        => new PersistentWhisperService();

    public static ITextInjector CreateTextInjector()
        => new TextInjector();
}
```

## Phase 3: Controllers (New Layer)
**Goal**: Extract orchestration logic from MainWindow

### Step 3.1: RecordingController
```csharp
namespace VoiceLite.Core.Controllers
{
    public class RecordingController : IRecordingController
    {
        private readonly IAudioRecorder _audioRecorder;
        private readonly IWhisperService _whisperService;
        private readonly ITextInjector _textInjector;
        private readonly IErrorLogger _errorLogger;

        public RecordingController(
            IAudioRecorder audioRecorder,
            IWhisperService whisperService,
            ITextInjector textInjector,
            IErrorLogger errorLogger)
        {
            _audioRecorder = audioRecorder;
            _whisperService = whisperService;
            _textInjector = textInjector;
            _errorLogger = errorLogger;
        }

        public async Task<TranscriptionResult> RecordAndTranscribeAsync(
            string modelPath,
            ITextInjector.InjectionMode injectionMode)
        {
            try
            {
                // Start recording
                _audioRecorder.StartRecording();

                // Wait for recording to complete
                var audioFile = await _audioRecorder.GetLastAudioFileAsync();

                // Transcribe
                var text = await _whisperService.TranscribeAsync(audioFile, modelPath);

                // Inject text
                await _textInjector.InjectTextAsync(text, injectionMode);

                return new TranscriptionResult { Success = true, Text = text };
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Recording and transcription failed");
                return new TranscriptionResult { Success = false, Error = ex.Message };
            }
        }
    }
}
```

## Phase 4: ViewModels (MVVM Pattern)
**Goal**: Move UI logic out of code-behind

### Step 4.1: Base ViewModel
```csharp
namespace VoiceLite.Presentation.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
```

### Step 4.2: MainViewModel
```csharp
namespace VoiceLite.Presentation.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IRecordingController _recordingController;
        private readonly ISettingsService _settingsService;
        private readonly IProFeatureService _proFeatureService;

        private bool _isRecording;
        private string _statusText = "Ready";
        private ObservableCollection<TranscriptionItem> _history;

        // Commands
        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        // Properties
        public bool IsRecording
        {
            get => _isRecording;
            set => SetField(ref _isRecording, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        public ObservableCollection<TranscriptionItem> History
        {
            get => _history;
            set => SetField(ref _history, value);
        }

        public MainViewModel(
            IRecordingController recordingController,
            ISettingsService settingsService,
            IProFeatureService proFeatureService)
        {
            _recordingController = recordingController;
            _settingsService = settingsService;
            _proFeatureService = proFeatureService;

            // Initialize commands
            StartRecordingCommand = new RelayCommand(ExecuteStartRecording, CanStartRecording);
            StopRecordingCommand = new RelayCommand(ExecuteStopRecording, CanStopRecording);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            ClearHistoryCommand = new RelayCommand(ExecuteClearHistory);

            // Initialize collections
            History = new ObservableCollection<TranscriptionItem>();
        }

        private async void ExecuteStartRecording()
        {
            IsRecording = true;
            StatusText = "Recording...";

            var result = await _recordingController.RecordAndTranscribeAsync(
                _settingsService.SelectedModel,
                _settingsService.InjectionMode);

            if (result.Success)
            {
                History.Insert(0, new TranscriptionItem
                {
                    Text = result.Text,
                    Timestamp = DateTime.Now
                });
                StatusText = "Transcription complete";
            }
            else
            {
                StatusText = $"Error: {result.Error}";
            }

            IsRecording = false;
        }

        private bool CanStartRecording() => !IsRecording;
        private void ExecuteStopRecording() => _recordingController.StopRecording();
        private bool CanStopRecording() => IsRecording;
    }
}
```

## Phase 5: Dependency Injection
**Goal**: Wire everything together with DI

### Step 5.1: Install Packages
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### Step 5.2: Configure DI in App.xaml.cs
```csharp
namespace VoiceLite
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;
        private IHost _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            _serviceProvider = _host.Services;

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Services (Singleton for stateful services)
            services.AddSingleton<IAudioRecorder, AudioRecorder>();
            services.AddSingleton<IWhisperService, PersistentWhisperService>();
            services.AddSingleton<ITextInjector, TextInjector>();
            services.AddSingleton<IErrorLogger, ErrorLogger>();
            services.AddSingleton<IHotkeyManager, HotkeyManager>();
            services.AddSingleton<ISystemTrayManager, SystemTrayManager>();

            // Register Features
            services.AddSingleton<ILicenseService, LicenseService>();
            services.AddSingleton<IProFeatureService, ProFeatureService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITranscriptionHistoryService, TranscriptionHistoryService>();

            // Register Controllers
            services.AddScoped<IRecordingController, RecordingController>();
            services.AddScoped<ITranscriptionController, TranscriptionController>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Register Windows
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindowNew>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
```

## Phase 6: MainWindow Refactoring
**Goal**: Reduce from 2,591 lines to ~200 lines

### Step 6.1: New MainWindow Structure
```csharp
namespace VoiceLite
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly ISystemTrayManager _systemTrayManager;

        public MainWindow(
            MainViewModel viewModel,
            IHotkeyManager hotkeyManager,
            ISystemTrayManager systemTrayManager)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _hotkeyManager = hotkeyManager;
            _systemTrayManager = systemTrayManager;

            DataContext = _viewModel;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Register hotkeys
            _hotkeyManager.RegisterHotkey(this);

            // Setup system tray
            _systemTrayManager.Initialize(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyManager?.Dispose();
            _systemTrayManager?.Dispose();
            base.OnClosed(e);
        }

        // Only UI-specific event handlers remain
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                _systemTrayManager.MinimizeToTray();
            }
        }
    }
}
```

## Migration Strategy

### Safe Migration Steps:
1. **Create new files** - Don't modify existing ones initially
2. **Run side-by-side** - New architecture alongside old
3. **Test incrementally** - Verify each component works
4. **Migrate gradually** - Move one feature at a time
5. **Delete old code** - Only after new code is tested

### Feature Migration Order:
1. Recording functionality (simplest)
2. Transcription pipeline
3. Settings management
4. History management
5. License/Pro features
6. System tray/hotkeys (most complex)

## Testing Each Phase

### Unit Test Example:
```csharp
[Fact]
public void RecordingController_ShouldOrchestrateWorkflow()
{
    // Arrange
    var mockRecorder = new Mock<IAudioRecorder>();
    var mockWhisper = new Mock<IWhisperService>();
    var mockInjector = new Mock<ITextInjector>();
    var mockLogger = new Mock<IErrorLogger>();

    mockRecorder.Setup(x => x.GetLastAudioFileAsync())
        .ReturnsAsync("test.wav");
    mockWhisper.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
        .ReturnsAsync("Test transcription");

    var controller = new RecordingController(
        mockRecorder.Object,
        mockWhisper.Object,
        mockInjector.Object,
        mockLogger.Object);

    // Act
    var result = await controller.RecordAndTranscribeAsync("model.bin", InjectionMode.Type);

    // Assert
    result.Success.Should().BeTrue();
    result.Text.Should().Be("Test transcription");
    mockInjector.Verify(x => x.InjectTextAsync("Test transcription", InjectionMode.Type), Times.Once);
}
```

## Daily Checkpoints

### Day 1 Checkpoint:
- [ ] All interfaces created
- [ ] Services implement interfaces
- [ ] ServiceFactory works
- [ ] App still runs unchanged

### Day 2 Checkpoint:
- [ ] Controllers created
- [ ] Controllers tested
- [ ] Can orchestrate workflow

### Day 3 Checkpoint:
- [ ] ViewModels created
- [ ] Commands work
- [ ] Data binding works

### Day 4 Checkpoint:
- [ ] DI configured
- [ ] App launches with DI
- [ ] Services injected correctly

### Day 5-7 Checkpoint:
- [ ] MainWindow < 300 lines
- [ ] All features working
- [ ] All tests passing
- [ ] No memory leaks

## Success Metrics

Before:
- MainWindow: 2,591 lines
- Testability: Poor
- Coupling: High
- Maintainability: Low

After:
- MainWindow: ~200 lines
- Testability: Excellent (all mockable)
- Coupling: Low (DI + interfaces)
- Maintainability: High (SOLID principles)

## Commands Reference

```bash
# Build frequently
dotnet build VoiceLite/VoiceLite.sln

# Test after each component
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Run app to verify
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Commit checkpoints
git add .
git commit -m "Week 2: [Component] - [Status]"
```