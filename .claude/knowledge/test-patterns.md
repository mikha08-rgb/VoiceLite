# Test Patterns for VoiceLite

## Testing Philosophy

### Coverage Targets
| Component | Target Coverage | Rationale |
|-----------|----------------|-----------|
| Overall | ≥ 75% | Industry standard for good coverage |
| Services/ | ≥ 80% | Core business logic, higher standard |
| Models/ | ≥ 90% | Data structures, easy to test |
| UI/XAML | ≥ 60% | Hard to test, integration tests preferred |
| API Routes | ≥ 75% | Critical for backend reliability |

### Testing Pyramid
```
         /\
        /  \  E2E Tests (5%)
       /----\
      /      \ Integration Tests (20%)
     /--------\
    /          \ Unit Tests (75%)
   /____________\
```

---

## xUnit Test Patterns

### Basic Test Structure (AAA Pattern)
```csharp
[Fact]
public void TranscribeAsync_WithValidAudio_ReturnsText()
{
    // Arrange (setup)
    var service = new PersistentWhisperService(settings);
    var audioPath = TestData.GetValidAudio();

    // Act (execute)
    var result = await service.TranscribeAsync(audioPath);

    // Assert (verify)
    result.Should().NotBeNullOrEmpty();
    result.Should().Contain("expected phrase");
}
```

### Theory Tests (Data-Driven)
```csharp
[Theory]
[InlineData("test.wav", true)]
[InlineData("missing.wav", false)]
[InlineData("", false)]
[InlineData(null, false)]
public void ValidateAudioPath_ReturnsExpectedResult(string path, bool expected)
{
    // Act
    var isValid = AudioValidator.IsValid(path);

    // Assert
    isValid.Should().Be(expected);
}
```

### Member Data Tests (Complex Data)
```csharp
public static IEnumerable<object[]> GetTestAudioFiles()
{
    yield return new object[] { "short.wav", 1.5, "Hello" };
    yield return new object[] { "medium.wav", 5.0, "This is a test" };
    yield return new object[] { "long.wav", 10.0, "Full sentence transcription" };
}

[Theory]
[MemberData(nameof(GetTestAudioFiles))]
public async Task TranscribeAsync_WithVariousLengths_ReturnsCorrectText(
    string fileName, double expectedDuration, string expectedText)
{
    // Arrange
    var audioPath = Path.Combine(TestData.AudioDir, fileName);

    // Act
    var result = await _service.TranscribeAsync(audioPath);

    // Assert
    result.Should().Contain(expectedText);
}
```

---

## Mocking with Moq

### Basic Mocking
```csharp
[Fact]
public async Task StartRecording_CallsRecorderStart()
{
    // Arrange
    var mockRecorder = new Mock<IAudioRecorder>();
    var mainWindow = new MainWindow(mockRecorder.Object);

    // Act
    await mainWindow.StartRecordingAsync();

    // Assert
    mockRecorder.Verify(r => r.StartRecording(), Times.Once);
}
```

### Setup Return Values
```csharp
[Fact]
public async Task ProcessTranscription_WithSuccessfulResult_UpdatesUI()
{
    // Arrange
    var mockTranscriber = new Mock<ITranscriber>();
    mockTranscriber
        .Setup(t => t.TranscribeAsync(It.IsAny<string>()))
        .ReturnsAsync("Hello world");

    var mainWindow = new MainWindow(Mock.Of<IAudioRecorder>(), mockTranscriber.Object);

    // Act
    await mainWindow.ProcessTranscriptionAsync("test.wav");

    // Assert
    mainWindow.StatusMessage.Should().Contain("Hello world");
}
```

### Verify Method Calls with Arguments
```csharp
[Fact]
public void ErrorOccurred_LogsToErrorLogger()
{
    // Arrange
    var mockLogger = new Mock<IErrorLogger>();
    ErrorLogger.SetInstance(mockLogger.Object);

    // Act
    try
    {
        ThrowException();
    }
    catch (Exception ex)
    {
        ErrorLogger.Log(ex);
    }

    // Assert
    mockLogger.Verify(
        l => l.Log(It.Is<Exception>(e => e.Message.Contains("expected message"))),
        Times.Once
    );
}
```

### Callback Testing
```csharp
[Fact]
public async Task RecordingCompleted_RaisesEvent()
{
    // Arrange
    var mockRecorder = new Mock<IAudioRecorder>();
    var eventRaised = false;

    mockRecorder
        .Setup(r => r.StopRecording())
        .Callback(() => {
            mockRecorder.Raise(r => r.RecordingCompleted += null, EventArgs.Empty);
        });

    var mainWindow = new MainWindow(mockRecorder.Object);
    mainWindow.RecordingCompleted += (s, e) => eventRaised = true;

    // Act
    await mainWindow.StopRecordingAsync();

    // Assert
    eventRaised.Should().BeTrue();
}
```

---

## FluentAssertions Patterns

### Basic Assertions
```csharp
// Strings
result.Should().NotBeNullOrEmpty();
result.Should().Contain("expected");
result.Should().StartWith("prefix");
result.Should().MatchRegex(@"\d{3}-\d{4}");

// Numbers
count.Should().BeGreaterThan(0);
latency.Should().BeLessThan(3000);
coverage.Should().BeInRange(75, 100);

// Collections
items.Should().NotBeEmpty();
items.Should().HaveCount(5);
items.Should().Contain(x => x.Name == "test");
items.Should().BeInAscendingOrder(x => x.Timestamp);

// Exceptions
Action act = () => ThrowException();
act.Should().Throw<InvalidOperationException>()
   .WithMessage("*expected message*");

// Objects
result.Should().NotBeNull();
result.Should().BeOfType<TranscriptionResult>();
result.Should().BeEquivalentTo(expected, options => options.Excluding(x => x.Timestamp));
```

### Chaining Assertions
```csharp
result.Should().NotBeNull()
    .And.BeOfType<Settings>()
    .Which.DefaultModel.Should().Be("ggml-small.bin");
```

### Async Assertions
```csharp
Func<Task> act = async () => await service.TranscribeAsync(null);
await act.Should().ThrowAsync<ArgumentNullException>();

await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
```

---

## Test Data Management

### Test Data Fixtures
```csharp
public class AudioTestData : IDisposable
{
    public string TestAudioPath { get; }
    public string OutputPath { get; }

    public AudioTestData()
    {
        // Create test audio file
        TestAudioPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.wav");
        GenerateTestAudio(TestAudioPath, duration: 3);

        OutputPath = Path.Combine(Path.GetTempPath(), "test_output");
        Directory.CreateDirectory(OutputPath);
    }

    public void Dispose()
    {
        // Clean up test files
        File.Delete(TestAudioPath);
        Directory.Delete(OutputPath, recursive: true);
    }
}

// Usage
public class WhisperServiceTests : IClassFixture<AudioTestData>
{
    private readonly AudioTestData _testData;

    public WhisperServiceTests(AudioTestData testData)
    {
        _testData = testData;
    }

    [Fact]
    public async Task TranscribeAsync_WithTestAudio_ReturnsText()
    {
        var result = await _service.TranscribeAsync(_testData.TestAudioPath);
        result.Should().NotBeEmpty();
    }
}
```

### Test Audio Generation
```csharp
public static class TestAudioGenerator
{
    public static void GenerateSilentWav(string path, int durationSeconds)
    {
        var sampleRate = 16000;
        var channels = 1;
        var bitsPerSample = 16;
        var samples = sampleRate * durationSeconds;

        using (var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, bitsPerSample, channels)))
        {
            byte[] silence = new byte[samples * (bitsPerSample / 8)];
            writer.Write(silence, 0, silence.Length);
        }
    }

    public static void GenerateToneWav(string path, int durationSeconds, double frequency)
    {
        var sampleRate = 16000;
        var amplitude = 0.3;

        using (var writer = new WaveFileWriter(path, new WaveFormat(sampleRate, 16, 1)))
        {
            for (int i = 0; i < sampleRate * durationSeconds; i++)
            {
                double t = i / (double)sampleRate;
                double value = amplitude * Math.Sin(2 * Math.PI * frequency * t);
                short sample = (short)(value * short.MaxValue);
                writer.WriteSample(sample);
            }
        }
    }
}
```

---

## Testing Async Code

### Basic Async Test
```csharp
[Fact]
public async Task TranscribeAsync_ReturnsText()
{
    // Act
    var result = await _service.TranscribeAsync(testAudio);

    // Assert
    result.Should().NotBeNullOrEmpty();
}
```

### Testing Task Cancellation
```csharp
[Fact]
public async Task TranscribeAsync_WhenCancelled_ThrowsOperationCanceledException()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.CancelAfter(100); // Cancel after 100ms

    // Act
    Func<Task> act = async () => await _service.TranscribeAsync(testAudio, cts.Token);

    // Assert
    await act.Should().ThrowAsync<OperationCanceledException>();
}
```

### Testing Async Event Handlers
```csharp
[Fact]
public async Task RecordingCompleted_TriggersTranscription()
{
    // Arrange
    var tcs = new TaskCompletionSource<string>();
    _mainWindow.TranscriptionCompleted += (s, e) => tcs.SetResult(e.Text);

    // Act
    await _mainWindow.StartRecordingAsync();
    await Task.Delay(100);
    await _mainWindow.StopRecordingAsync();

    // Assert
    var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    result.Should().NotBeNullOrEmpty();
}
```

---

## Testing WPF UI

### Testing Dispatcher Operations
```csharp
[Fact]
public async Task UpdateStatus_UpdatesUIOnDispatcherThread()
{
    // Arrange
    var mainWindow = new MainWindow();
    await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.Show());

    // Act
    await mainWindow.Dispatcher.InvokeAsync(() => {
        mainWindow.UpdateStatus("Test message");
    });

    // Assert
    await mainWindow.Dispatcher.InvokeAsync(() => {
        mainWindow.StatusMessage.Should().Be("Test message");
    });
}
```

### Testing Data Binding
```csharp
[Fact]
public void ViewModel_PropertyChanged_UpdatesUI()
{
    // Arrange
    var viewModel = new MainWindowViewModel();
    var propertyChangedRaised = false;
    viewModel.PropertyChanged += (s, e) => {
        if (e.PropertyName == nameof(viewModel.StatusMessage))
            propertyChangedRaised = true;
    };

    // Act
    viewModel.StatusMessage = "New status";

    // Assert
    propertyChangedRaised.Should().BeTrue();
}
```

---

## Integration Testing

### Testing API Routes (Next.js)
```typescript
// tests/api/checkout.test.ts
import { POST } from '@/app/api/checkout/route';

describe('POST /api/checkout', () => {
  it('creates checkout session with valid data', async () => {
    // Arrange
    const req = new Request('http://localhost:3000/api/checkout', {
      method: 'POST',
      body: JSON.stringify({
        email: 'test@example.com',
        plan: 'subscription',
      }),
    });

    // Act
    const response = await POST(req);
    const data = await response.json();

    // Assert
    expect(response.status).toBe(200);
    expect(data.url).toMatch(/^https:\/\/checkout\.stripe\.com/);
  });

  it('returns 400 for invalid email', async () => {
    // Arrange
    const req = new Request('http://localhost:3000/api/checkout', {
      method: 'POST',
      body: JSON.stringify({
        email: 'invalid-email',
        plan: 'subscription',
      }),
    });

    // Act
    const response = await POST(req);

    // Assert
    expect(response.status).toBe(400);
  });
});
```

### Testing Database Operations (Prisma)
```typescript
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

describe('License CRUD', () => {
  afterEach(async () => {
    // Clean up test data
    await prisma.license.deleteMany({
      where: { email: { contains: 'test@' } },
    });
  });

  it('creates license successfully', async () => {
    // Arrange
    const licenseData = {
      email: 'test@example.com',
      stripeSubscriptionId: 'sub_test123',
      status: 'active',
    };

    // Act
    const license = await prisma.license.create({ data: licenseData });

    // Assert
    expect(license.id).toBeDefined();
    expect(license.email).toBe('test@example.com');
  });
});
```

---

## Testing Error Handling

### Testing Exception Throwing
```csharp
[Fact]
public void TranscribeAsync_WithNullPath_ThrowsArgumentNullException()
{
    // Act
    Func<Task> act = async () => await _service.TranscribeAsync(null);

    // Assert
    await act.Should().ThrowAsync<ArgumentNullException>()
        .WithParameterName("audioPath");
}
```

### Testing Error Recovery
```csharp
[Fact]
public async Task TranscribeAsync_WhenProcessCrashes_RetriesOnce()
{
    // Arrange
    var mockProcess = new Mock<IProcess>();
    mockProcess
        .SetupSequence(p => p.Start())
        .Throws<InvalidOperationException>() // First attempt fails
        .Returns(true);                      // Retry succeeds

    // Act
    var result = await _service.TranscribeAsync(testAudio);

    // Assert
    result.Should().NotBeEmpty();
    mockProcess.Verify(p => p.Start(), Times.Exactly(2));
}
```

### Testing Graceful Degradation
```csharp
[Fact]
public async Task LoadSettings_WhenFileCorrupted_ReturnsDefaults()
{
    // Arrange
    File.WriteAllText(settingsPath, "invalid json{{{");

    // Act
    var settings = await Settings.LoadAsync();

    // Assert
    settings.Should().NotBeNull();
    settings.DefaultModel.Should().Be("ggml-small.bin"); // Default value
}
```

---

## Performance Testing

### Testing Latency
```csharp
[Fact]
public async Task TranscribeAsync_CompletesWithin3Seconds()
{
    // Arrange
    var testAudio = TestData.GetShortAudio(); // 5-second clip

    // Act
    var stopwatch = Stopwatch.StartNew();
    await _service.TranscribeAsync(testAudio);
    stopwatch.Stop();

    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000);
}
```

### Testing Memory Leaks
```csharp
[Fact]
public void RepeatedTranscriptions_DoNotLeakMemory()
{
    // Arrange
    var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

    // Act
    for (int i = 0; i < 100; i++)
    {
        _service.TranscribeAsync(testAudio).Wait();
    }
    GC.Collect();
    GC.WaitForPendingFinalizers();
    var finalMemory = GC.GetTotalMemory(forceFullCollection: true);

    // Assert
    var leakMB = (finalMemory - initialMemory) / 1024 / 1024;
    leakMB.Should().BeLessThan(50); // Max 50MB growth
}
```

---

## Test Organization

### Test File Structure
```
VoiceLite.Tests/
├── Services/
│   ├── AudioRecorderTests.cs
│   ├── PersistentWhisperServiceTests.cs
│   └── TextInjectorTests.cs
├── Models/
│   ├── SettingsTests.cs
│   └── TranscriptionResultTests.cs
├── Integration/
│   ├── EndToEndTranscriptionTests.cs
│   └── ApiIntegrationTests.cs
├── TestData/
│   ├── audio/
│   │   ├── short.wav
│   │   ├── medium.wav
│   │   └── long.wav
│   └── AudioTestData.cs
└── Helpers/
    ├── TestAudioGenerator.cs
    └── MockHelpers.cs
```

### Test Categories
```csharp
// Unit tests
[Trait("Category", "Unit")]
public class AudioRecorderTests { }

// Integration tests
[Trait("Category", "Integration")]
public class EndToEndTests { }

// Performance tests
[Trait("Category", "Performance")]
public class PerformanceTests { }

// Run specific category
// dotnet test --filter "Category=Unit"
```

---

## Common Testing Anti-Patterns

### ❌ Avoid: Testing Implementation Details
```csharp
// BAD - Tests private method directly
[Fact]
public void ParseWhisperOutput_Internal_ParsesCorrectly() { }

// GOOD - Tests public behavior
[Fact]
public async Task TranscribeAsync_ReturnsCleanedText() { }
```

### ❌ Avoid: Fragile Tests
```csharp
// BAD - Breaks when unrelated property changes
result.Should().BeEquivalentTo(expected);

// GOOD - Tests only relevant properties
result.Text.Should().Be(expected.Text);
result.Confidence.Should().BeGreaterThan(0.8);
```

### ❌ Avoid: Test Interdependence
```csharp
// BAD - Test2 depends on Test1's side effects
[Fact]
public void Test1_CreatesFile() {
    File.WriteAllText("shared.txt", "data");
}

[Fact]
public void Test2_ReadsFile() {
    var content = File.ReadAllText("shared.txt"); // Fragile!
}

// GOOD - Each test is independent
[Fact]
public void Test1_CreatesAndReadsOwnFile() {
    var path = Path.GetTempFileName();
    File.WriteAllText(path, "data");
    var content = File.ReadAllText(path);
    File.Delete(path);
}
```

---

## Code Coverage Reporting

### Generate Coverage Report
```bash
# Run tests with coverage
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --settings VoiceLite/VoiceLite.Tests/coverlet.runsettings

# Coverage report location
# VoiceLite/VoiceLite.Tests/TestResults/{guid}/coverage.cobertura.xml
```

### Parse Coverage Report
```csharp
public class CoverageParser
{
    public static CoverageStats ParseCobertura(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath);
        var coverage = doc.Root.Attribute("line-rate").Value;

        return new CoverageStats
        {
            OverallCoverage = double.Parse(coverage) * 100,
            // Parse line coverage by directory...
        };
    }
}
```

---

## Testing Checklist

Before marking a feature complete:
- [ ] Unit tests written for all new public methods
- [ ] Integration test covers end-to-end scenario
- [ ] Edge cases tested (null, empty, invalid input)
- [ ] Error handling tested (exceptions caught/thrown)
- [ ] Async code tested (cancellation, timeouts)
- [ ] Performance tested if latency-sensitive
- [ ] Memory leaks checked if resource-intensive
- [ ] Coverage targets met (≥75% overall, ≥80% Services/)
- [ ] All tests pass locally
- [ ] Tests pass in CI/CD pipeline

---

## References
- xUnit Documentation: https://xunit.net/
- Moq Quickstart: https://github.com/moq/moq4/wiki/Quickstart
- FluentAssertions: https://fluentassertions.com/
- Coverlet (Coverage): https://github.com/coverlet-coverage/coverlet
