# Script to add [Trait("Category", "...")] attributes to test classes
# Usage: .\scripts\add-test-categories.ps1

param(
    [string]$TestsRoot = "VoiceLite\VoiceLite.Tests"
)

Write-Host "Adding test category traits..." -ForegroundColor Cyan

# Unit test files (add class-level [Trait("Category", "Unit")])
$unitTestFiles = @(
    "AppProcessDetectionTests.cs",
    "Models\SettingsTests.cs",
    "Models\TranscriptionHistoryItemTests.cs",
    "Models\WhisperModelInfoTests.cs",
    "Utilities\StatusColorsTests.cs",
    "Utilities\TimingConstantsTests.cs",
    "Utilities\TextAnalyzerTests.cs",
    "Utilities\RelativeTimeConverterTests.cs",
    "Utilities\TruncateTextConverterTests.cs",
    "Services\TextInjectorTests.cs",
    "Services\HotkeyManagerTests.cs",
    "Services\TranscriptionHistoryServiceTests.cs",
    "Services\DependencyCheckerTests.cs",
    "Services\StartupDiagnosticsTests.cs",
    "Services\ZombieProcessCleanupServiceTests.cs",
    "Services\WhisperErrorRecoveryTests.cs",
    "Services\SoundServiceTests.cs"
)

# Hardware test files (add class-level [Trait("Category", "Hardware")])
$hardwareTestFiles = @(
    "Services\AudioRecorderTests.cs",
    "Services\MemoryMonitorTests.cs"
)

# FileIO test files (add class-level [Trait("Category", "FileIO")])
$fileIOTestFiles = @(
    "Services\AudioPreprocessorTests.cs",
    "Services\ErrorLoggerTests.cs"
)

# Function to add trait to class
function Add-ClassTrait {
    param(
        [string]$FilePath,
        [string]$Category
    )

    $content = Get-Content $FilePath -Raw

    # Check if trait already exists
    if ($content -match "\[Trait\(`"Category`", `"$Category`"\)\]") {
        Write-Host "  SKIP: $FilePath (already has trait)" -ForegroundColor Yellow
        return
    }

    # Find the class declaration line
    $pattern = "(    public class \w+Tests)"
    if ($content -match $pattern) {
        $replacement = "    [Trait(`"Category`", `"$Category`")]`n    public class"
        $newContent = $content -replace $pattern, $replacement

        Set-Content -Path $FilePath -Value $newContent -NoNewline
        Write-Host "  OK: $FilePath → $Category" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $FilePath (class not found)" -ForegroundColor Red
    }
}

# Apply Unit category
Write-Host "`nApplying Unit category..." -ForegroundColor Cyan
foreach ($file in $unitTestFiles) {
    $path = Join-Path $TestsRoot $file
    if (Test-Path $path) {
        Add-ClassTrait -FilePath $path -Category "Unit"
    } else {
        Write-Host "  WARN: $file not found" -ForegroundColor Yellow
    }
}

# Apply Hardware category
Write-Host "`nApplying Hardware category..." -ForegroundColor Cyan
foreach ($file in $hardwareTestFiles) {
    $path = Join-Path $TestsRoot $file
    if (Test-Path $path) {
        Add-ClassTrait -FilePath $path -Category "Hardware"
    } else {
        Write-Host "  WARN: $file not found" -ForegroundColor Yellow
    }
}

# Apply FileIO category
Write-Host "`nApplying FileIO category..." -ForegroundColor Cyan
foreach ($file in $fileIOTestFiles) {
    $path = Join-Path $TestsRoot $file
    if (Test-Path $path) {
        Add-ClassTrait -FilePath $path -Category "FileIO"
    } else {
        Write-Host "  WARN: $file not found" -ForegroundColor Yellow
    }
}

Write-Host "`nDone! Run 'dotnet test --list-tests --filter Category=Unit' to verify." -ForegroundColor Cyan
