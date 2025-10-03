# WPF/XAML Patterns for VoiceLite

## Thread Safety in WPF

### The Golden Rule
**UI elements can only be accessed from the UI thread (Dispatcher thread).**

### Common Violation Pattern
```csharp
// ❌ WRONG - Will throw InvalidOperationException
private void OnBackgroundEvent(string message)
{
    lblStatus.Content = message; // Called from background thread!
}
```

### Correct Pattern with Dispatcher
```csharp
// ✅ CORRECT
private void OnBackgroundEvent(string message)
{
    Dispatcher.Invoke(() => {
        lblStatus.Content = message;
    });
}

// ✅ ALSO CORRECT (async version)
private async void OnBackgroundEvent(string message)
{
    await Dispatcher.InvokeAsync(() => {
        lblStatus.Content = message;
    });
}
```

### When to Use Dispatcher
Use `Dispatcher.Invoke()` when:
- Updating UI elements from service callbacks
- Handling events from background threads
- Processing async results that update UI
- Working with timers (System.Timers.Timer callbacks)

Don't use `Dispatcher.Invoke()` when:
- Already on UI thread (check with `Dispatcher.CheckAccess()`)
- No UI updates needed
- Processing pure business logic

### Performance Optimization
```csharp
// Batch multiple UI updates in single Dispatcher call
await Dispatcher.InvokeAsync(() => {
    lblStatus.Content = "Processing...";
    progressBar.Value = 50;
    btnStart.IsEnabled = false;
});

// Don't do this (3 separate marshalling calls):
await Dispatcher.InvokeAsync(() => lblStatus.Content = "Processing...");
await Dispatcher.InvokeAsync(() => progressBar.Value = 50);
await Dispatcher.InvokeAsync(() => btnStart.IsEnabled = false);
```

## MVVM Pattern (Recommended)

### Benefits
- Better separation of concerns
- Easier unit testing
- Cleaner code-behind
- Better maintainability

### Basic MVVM Structure
```
MainWindow.xaml          (View - UI definition)
MainWindow.xaml.cs       (View - minimal code-behind)
MainWindowViewModel.cs   (ViewModel - presentation logic)
RecordingService.cs      (Model - business logic)
```

### ViewModel Example
```csharp
public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordButtonText));
            }
        }
    }

    public string RecordButtonText => IsRecording ? "Stop" : "Record";

    public ICommand ToggleRecordingCommand { get; }

    public MainWindowViewModel()
    {
        ToggleRecordingCommand = new RelayCommand(ToggleRecording);
    }

    private void ToggleRecording()
    {
        IsRecording = !IsRecording;
        // Business logic...
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

### XAML Binding
```xaml
<Window DataContext="{Binding Source={StaticResource MainWindowViewModel}}">
    <TextBlock Text="{Binding StatusMessage}" />
    <Button Content="{Binding RecordButtonText}"
            Command="{Binding ToggleRecordingCommand}" />
</Window>
```

## Resource Disposal Patterns

### IDisposable Implementation
```csharp
public class MainWindow : Window, IDisposable
{
    private AudioRecorder _recorder;
    private ITranscriber _transcriber;
    private HotkeyManager _hotkeyManager;
    private Timer _timer;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        // Unsubscribe from events (prevent memory leaks)
        if (_hotkeyManager != null)
        {
            _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
        }

        // Dispose managed resources
        _recorder?.Dispose();
        _transcriber?.Dispose();
        _hotkeyManager?.Dispose();
        _timer?.Dispose();

        _disposed = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }
}
```

### Common Memory Leaks in WPF

#### 1. Event Handler Leaks
```csharp
// ❌ LEAK - Event never unsubscribed
public MainWindow()
{
    GlobalEventManager.SomeEvent += OnEvent;
}

// ✅ FIXED
public MainWindow()
{
    GlobalEventManager.SomeEvent += OnEvent;
    Closed += (s, e) => GlobalEventManager.SomeEvent -= OnEvent;
}
```

#### 2. Timer Leaks
```csharp
// ❌ LEAK - Timer not disposed
private Timer _timer = new Timer(1000);

// ✅ FIXED
private Timer _timer;
public void Dispose()
{
    _timer?.Stop();
    _timer?.Dispose();
}
```

#### 3. Static Event Leaks
```csharp
// ❌ LEAK - Window subscribes to static event, never unsubscribes
public MainWindow()
{
    ApplicationEvents.StatusChanged += OnStatusChanged;
}

// ✅ FIXED
public MainWindow()
{
    ApplicationEvents.StatusChanged += OnStatusChanged;
    Unloaded += (s, e) => ApplicationEvents.StatusChanged -= OnStatusChanged;
}
```

## Null Safety Patterns

### Guard Clauses
```csharp
public void ProcessTranscription(string text)
{
    // ✅ GOOD - Early return
    if (string.IsNullOrEmpty(text))
    {
        ErrorLogger.Log("Transcription text is null or empty");
        return;
    }

    // ... rest of method
}
```

### Null-Conditional Operators
```csharp
// ✅ Safe service invocation
_recorder?.StopRecording();

// ✅ Safe event raising
StatusChanged?.Invoke(this, EventArgs.Empty);

// ✅ Safe property access
var isRecording = _recorder?.IsRecording ?? false;
```

### Null-Forgiving Operator (Use Sparingly)
```csharp
// Use only when you're 100% sure the value is not null
var settings = Settings.Load()!; // ! means "trust me, it's not null"
```

## Async/Await Best Practices

### Async Void (Event Handlers Only)
```csharp
// ✅ ACCEPTABLE - Event handler
private async void btnStart_Click(object sender, RoutedEventArgs e)
{
    await StartRecordingAsync();
}

// ❌ WRONG - Regular method
private async void ProcessData() // Should return Task
{
    await Task.Delay(1000);
}

// ✅ CORRECT - Regular method
private async Task ProcessDataAsync()
{
    await Task.Delay(1000);
}
```

### ConfigureAwait in UI Context
```csharp
// In WPF applications, DON'T use ConfigureAwait(false) if you need to update UI
private async Task UpdateUIAsync()
{
    var result = await GetDataAsync(); // Continues on UI thread
    lblResult.Text = result;           // Safe - still on UI thread
}

// Use ConfigureAwait(false) only for pure logic (no UI updates)
private async Task<string> ProcessDataAsync()
{
    await Task.Delay(1000).ConfigureAwait(false); // No need for UI thread
    return "Processed";
}
```

### Async Method Naming
```csharp
// ✅ GOOD - Async methods end with "Async"
public async Task<string> TranscribeAsync(string audioPath) { }
public async Task SaveSettingsAsync() { }

// ❌ BAD - Misleading name
public async Task Transcribe() { }
```

## Data Binding Patterns

### OneWay vs TwoWay Binding
```xaml
<!-- OneWay: Updates UI when property changes -->
<TextBlock Text="{Binding StatusMessage, Mode=OneWay}" />

<!-- TwoWay: Updates property when user edits -->
<TextBox Text="{Binding UserName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

<!-- Default mode varies by control:
     - TextBlock: OneWay
     - TextBox: TwoWay -->
```

### Value Converters
```csharp
// BooleanToVisibilityConverter example
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (Visibility)value == Visibility.Visible;
    }
}
```

```xaml
<!-- Usage in XAML -->
<Window.Resources>
    <local:BoolToVisibilityConverter x:Key="BoolToVis" />
</Window.Resources>

<Border Visibility="{Binding IsRecording, Converter={StaticResource BoolToVis}}" />
```

## Dependency Injection in WPF

### Constructor Injection (Preferred)
```csharp
public class MainWindow : Window
{
    private readonly IAudioRecorder _recorder;
    private readonly ITranscriber _transcriber;

    // Constructor injection
    public MainWindow(IAudioRecorder recorder, ITranscriber transcriber)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));

        InitializeComponent();
    }
}

// App.xaml.cs setup
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var recorder = new AudioRecorder();
    var transcriber = new PersistentWhisperService(Settings.Load());
    var mainWindow = new MainWindow(recorder, transcriber);
    mainWindow.Show();
}
```

## Performance Optimization

### Virtualization for Large Lists
```xaml
<!-- Use VirtualizingStackPanel for long lists -->
<ListBox VirtualizingStackPanel.IsVirtualizing="True"
         VirtualizingStackPanel.VirtualizationMode="Recycling">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
```

### Freeze Freezable Resources
```csharp
// Freeze immutable resources for better performance
var brush = new SolidColorBrush(Colors.Red);
brush.Freeze(); // Makes it thread-safe and faster
myControl.Background = brush;
```

### Avoid Overusing DataBinding
```csharp
// ❌ SLOW - Binding for static data
<TextBlock Text="{Binding ConstantTitle}" />

// ✅ FAST - Direct value for static data
<TextBlock Text="VoiceLite Settings" />
```

## Common WPF Gotchas

### 1. Window ShowDialog Blocks Thread
```csharp
// ❌ WRONG - Blocks UI thread
var dialog = new SettingsWindow();
dialog.ShowDialog(); // Blocks until closed

// ✅ BETTER - Use modeless window if possible
var dialog = new SettingsWindow();
dialog.Show(); // Non-blocking
```

### 2. Binding to Non-Public Properties
```csharp
// ❌ WON'T WORK - Binding requires public properties
private string StatusMessage { get; set; }

// ✅ WORKS
public string StatusMessage { get; set; }
```

### 3. Forgetting InitializeComponent()
```csharp
public MainWindow()
{
    InitializeComponent(); // ✅ MUST be first line
    // ... other initialization
}
```

### 4. Window Startup Location
```xaml
<!-- Center window on screen -->
<Window WindowStartupLocation="CenterScreen" />

<!-- Or in code -->
this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
```

## Accessibility Best Practices

### AutomationProperties
```xaml
<!-- Screen reader support -->
<Button AutomationProperties.Name="Start Recording"
        AutomationProperties.HelpText="Press to start voice recording" />

<TextBox AutomationProperties.LabeledBy="{Binding ElementName=lblUserName}" />
<Label x:Name="lblUserName" Content="User Name:" />
```

### Keyboard Navigation
```xaml
<!-- Tab order -->
<TextBox TabIndex="1" />
<Button TabIndex="2" />
<ComboBox TabIndex="3" />

<!-- Keyboard shortcuts -->
<Button Content="_Start" /> <!-- Alt+S triggers button -->
```

## Testing Patterns

### Test UI Logic Separately
```csharp
// ❌ HARD TO TEST
private void btnStart_Click(object sender, RoutedEventArgs e)
{
    // Business logic mixed with UI code
    var audioPath = RecordAudio();
    var text = TranscribeAudio(audioPath);
    lblResult.Text = text;
}

// ✅ EASY TO TEST
private void btnStart_Click(object sender, RoutedEventArgs e)
{
    var result = ProcessRecording(); // Testable method
    DisplayResult(result);
}

private string ProcessRecording() // Pure logic - unit testable
{
    var audioPath = RecordAudio();
    return TranscribeAudio(audioPath);
}

private void DisplayResult(string result) // UI update - integration test
{
    lblResult.Text = result;
}
```

## References
- WPF Documentation: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/
- MVVM Pattern: https://docs.microsoft.com/en-us/windows/uwp/data-binding/data-binding-and-mvvm
- Thread Safety: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model
