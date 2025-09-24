param(
    [Parameter(Mandatory = $true)]
    [string]$AudioDirectory,

    [string[]]$Models = @("ggml-small.bin", "ggml-medium.bin"),

    [string]$OutputCsv = "qa-results.csv",

    [int]$BeamSize = 5,

    [int]$BestOf = 5,

    [string]$Language = "en"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$whisperExe = Join-Path $scriptDir '..\VoiceLite\VoiceLite\whisper\whisper.exe'
$whisperExe = [System.IO.Path]::GetFullPath($whisperExe)

if (-not (Test-Path $whisperExe)) {
    Write-Error "Unable to locate whisper.exe at $whisperExe. Ensure models are downloaded via the app.";
    exit 1
}

$audioDir = [System.IO.Path]::GetFullPath($AudioDirectory)
if (-not (Test-Path $audioDir)) {
    Write-Error "Audio directory '$audioDir' was not found."
    exit 1
}

$audioFiles = Get-ChildItem -Path $audioDir -Filter *.wav -File -Recurse
if ($audioFiles.Count -eq 0) {
    Write-Error "No .wav files found under '$audioDir'."
    exit 1
}

$results = @()

foreach ($file in $audioFiles) {
    foreach ($model in $Models) {
        $modelPath = Join-Path $scriptDir "..\VoiceLite\VoiceLite\whisper\$model"
        $modelPath = [System.IO.Path]::GetFullPath($modelPath)
        if (-not (Test-Path $modelPath)) {
            Write-Warning "Model '$model' is not present in the whisper directory. Skipping."
            continue
        }

        Write-Host "Transcribing $($file.Name) with $model..." -ForegroundColor Cyan

        $args = @('-m', $modelPath, '-f', $file.FullName, '--no-timestamps', '--threads', [Environment]::ProcessorCount)
        if ($BeamSize -gt 0) { $args += @('--beam-size', $BeamSize) }
        if ($BestOf -gt 0) { $args += @('--best-of', $BestOf) }
        if (-not [string]::IsNullOrWhiteSpace($Language)) { $args += @('--language', $Language) }
        $args += @('--entropy-thold', '2.4', '--logprob-thold', '-1.0')

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $output = & $whisperExe @args 2>&1
        $exitCode = $LASTEXITCODE
        $stopwatch.Stop()

        $transcription = New-Object System.Text.StringBuilder
        foreach ($line in $output) {
            $trimmed = $line.Trim()
            if ([string]::IsNullOrEmpty($trimmed)) { continue }
            if ($trimmed.StartsWith('[') -and $trimmed.Contains(']')) { continue }
            if ($trimmed -match 'whisper_' -or $trimmed -match 'system_info' -or $trimmed -match 'model size' -or $trimmed -match 'processing' -or $trimmed -match 'thread') { continue }
            [void]$transcription.Append($trimmed + ' ')
        }

        $results += [PSCustomObject]@{
            Audio          = $file.FullName
            Model          = $model
            DurationMs     = $stopwatch.ElapsedMilliseconds
            ExitCode       = $exitCode
            Transcript     = $transcription.ToString().Trim()
            RawOutput      = ($output -join "`n")
        }
    }
}

$csvPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDir $OutputCsv))
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-Host "QA comparison complete. Results saved to $csvPath" -ForegroundColor Green
Write-Host "Tip: Load the CSV in Excel or VS Code diff to compare transcripts across models." -ForegroundColor Yellow
