# Week 2: Extract & Modularize (MVVM + DI)

## Week 1 Recap ✅
- Fixed resource leaks (HttpClient, Timers)
- Added async safety (AsyncHelper)
- Created integration tests
- **Foundation is now stable!**

## Week 2 Goals

Transform the 2,591-line MainWindow into clean MVVM architecture with dependency injection.

### Target: MainWindow from 2,591 → ~200 lines

---

## Day 1-2: Create Interfaces

### Files to Create:
```
VoiceLite/Core/Interfaces/
├── IAudioRecorder.cs
├── IWhisperService.cs
├── ITextInjector.cs
├── IHotkeyManager.cs
├── ILicenseService.cs
├── ISettingsService.cs
└── ISystemTrayManager.cs
```

### Example Interface:
```csharp
namespace VoiceLite.Core.Interfaces
{
    public interface IAudioRecorder : IDisposable
    {
        bool IsRecording { get; }
        event EventHandler<string> AudioFileReady;
        void StartRecording();
        void StopRecording();
    }
}
```

---

## Day 3: Extract Controllers

### Files to Create:
```
VoiceLite/Core/Controllers/
├── RecordingController.cs      # Orchestrates recording + transcription
├── SettingsController.cs       # Manages settings persistence
└── TranscriptionController.cs  # Handles transcription workflow
```

### RecordingController Example:
```csharp
public class RecordingController
{
    private readonly IAudioRecorder _audioRecorder;
    private readonly IWhisperService _whisperService;
    private readonly ITextInjector _textInjector;

    public async Task<string> RecordAndTranscribeAsync()
    {
        // Orchestrate the full workflow
    }
}
```

---

## Day 4: Create ViewModels

### Files to Create:
```
VoiceLite/Presentation/ViewModels/
├── MainViewModel.cs         # Main window logic
├── RecordingViewModel.cs    # Recording state
└── SettingsViewModel.cs     # Settings binding
```

### MainViewModel Structure:
```csharp
public class MainViewModel : INotifyPropertyChanged
{
    // Commands
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }

    // Properties
    public bool IsRecording { get; set; }
    public string StatusText { get; set; }
    public ObservableCollection<TranscriptionItem> History { get; }

    // Dependency Injection
    public MainViewModel(
        IRecordingController recordingController,
        ISettingsController settingsController)
    {
        // Initialize
    }
}
```

---

## Day 5: Add Dependency Injection

### Install Package:
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### Configure DI in App.xaml.cs:
```csharp
public partial class App : Application
{
    private ServiceProvider _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IAudioRecorder, AudioRecorder>();
        services.AddSingleton<IWhisperService, PersistentWhisperService>();
        services.AddSingleton<ITextInjector, TextInjector>();

        // Register ViewModels
        services.AddTransient<MainViewModel>();

        // Register Windows
        services.AddTransient<MainWindow>();
    }
}
```

---

## Day 6-7: Refactor MainWindow

### Current MainWindow (2,591 lines):
- UI event handlers
- Business logic
- Service management
- Settings handling
- Timer management
- State management

### New MainWindow (~200 lines):
```csharp
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    // Only UI-specific code remains
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _viewModel.Initialize(this);
    }
}
```

---

## File Structure After Week 2

```
VoiceLite/
├── Core/
│   ├── Interfaces/
│   │   ├── IAudioRecorder.cs
│   │   ├── IWhisperService.cs
│   │   └── ITextInjector.cs
│   ├── Controllers/
│   │   ├── RecordingController.cs
│   │   └── SettingsController.cs
│   └── Models/
│       └── Settings.cs
├── Infrastructure/
│   └── Services/           # Existing services implement interfaces
│       ├── AudioRecorder.cs : IAudioRecorder
│       ├── PersistentWhisperService.cs : IWhisperService
│       └── TextInjector.cs : ITextInjector
├── Presentation/
│   ├── ViewModels/
│   │   └── MainViewModel.cs
│   ├── Views/
│   │   └── MainWindow.xaml
│   └── Converters/
│       └── BoolToVisibilityConverter.cs
└── App.xaml.cs             # DI configuration
```

---

## Testing Strategy

### For Each Day:
1. Create interfaces/classes
2. Write unit tests
3. Refactor existing code
4. Verify tests still pass
5. Commit changes

### Test Example:
```csharp
[Fact]
public void MainViewModel_StartRecording_ShouldUpdateState()
{
    // Arrange
    var mockRecorder = new Mock<IAudioRecorder>();
    var viewModel = new MainViewModel(mockRecorder.Object);

    // Act
    viewModel.StartRecordingCommand.Execute(null);

    // Assert
    viewModel.IsRecording.Should().BeTrue();
    mockRecorder.Verify(x => x.StartRecording(), Times.Once);
}
```

---

## Success Metrics

### After Week 2:
- ✅ MainWindow: 2,591 → ~200 lines
- ✅ All services behind interfaces
- ✅ Dependency injection configured
- ✅ MVVM pattern implemented
- ✅ Unit testable architecture
- ✅ Controllers orchestrate workflows

---

## Commands for Week 2

```bash
# Add packages
dotnet add VoiceLite/VoiceLite/VoiceLite.csproj package Microsoft.Extensions.DependencyInjection
dotnet add VoiceLite/VoiceLite/VoiceLite.csproj package Microsoft.Extensions.Hosting

# Build frequently
dotnet build VoiceLite/VoiceLite.sln

# Test after each change
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Commit daily
git add .
git commit -m "Week 2 Day X: [description]"
```

---

## Daily Commits

- Day 1: "Week 2: Add service interfaces"
- Day 2: "Week 2: Complete interface definitions"
- Day 3: "Week 2: Add controllers layer"
- Day 4: "Week 2: Implement ViewModels"
- Day 5: "Week 2: Add dependency injection"
- Day 6-7: "Week 2: Refactor MainWindow to MVVM"

---

## Ready to Start?

**Week 2 will transform your codebase from monolithic to modular.**

Benefits:
- Easy to test (mock interfaces)
- Easy to modify (single responsibility)
- Easy to understand (clear separation)
- Easy to extend (just add interfaces)