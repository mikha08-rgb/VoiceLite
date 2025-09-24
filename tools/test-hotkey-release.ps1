param(
    [string]$LogPath = Join-Path (Split-Path $PSScriptRoot -Parent) "VoiceLite\VoiceLite\voicelite_error.log",
    [int]$ThresholdMs = 250
)

if (-not (Test-Path $LogPath)) {
    Write-Error "Log file not found at '$LogPath'. Launch VoiceLite once or update the path." -ErrorAction Stop
}

$lines = Get-Content -Path $LogPath
if ($lines.Count -eq 0) {
    Write-Warning "Log file is empty. Make sure logging is enabled and exercise the hotkey."
    return
}

$pressPattern = '^[\[](?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})[]] OnHotkeyPressed: Entry'
$releasePattern = '^[\[](?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})[]] OnHotkeyReleased: Stopping recording'

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$pressEvents = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match $pressPattern) {
        $pressEvents += [pscustomobject]@{
            Index = $i
            Timestamp = [DateTime]::ParseExact($matches.ts, 'yyyy-MM-dd HH:mm:ss', $culture)
            Line = $line
        }
    }
}

if ($pressEvents.Count -eq 0) {
    Write-Warning "No 'OnHotkeyPressed' entries found in log."
    return
}

$pressToCheck = $null
$releaseEvent = $null
foreach ($press in ($pressEvents | Sort-Object Index -Descending)) {
    for ($j = $press.Index + 1; $j -lt $lines.Count; $j++) {
        $candidate = $lines[$j]
        if ($candidate -match $releasePattern) {
            $pressToCheck = $press
            $releaseEvent = [pscustomobject]@{
                Index = $j
                Timestamp = [DateTime]::ParseExact($matches.ts, 'yyyy-MM-dd HH:mm:ss', $culture)
                Line = $candidate
            }
            break
        }
    }
    if ($pressToCheck) { break }
}

if (-not $pressToCheck -or -not $releaseEvent) {
    Write-Warning "No matching 'OnHotkeyReleased: Stopping recording' entry found after the latest press."
    return
}

$deltaMs = [int](($releaseEvent.Timestamp - $pressToCheck.Timestamp).TotalMilliseconds)

Write-Host "Hotkey press : $($pressToCheck.Timestamp.ToString('HH:mm:ss.fff'))" -ForegroundColor Cyan
Write-Host "Hotkey release: $($releaseEvent.Timestamp.ToString('HH:mm:ss.fff'))" -ForegroundColor Cyan
Write-Host "Elapsed      : ${deltaMs} ms" -ForegroundColor Cyan

if ($deltaMs -le $ThresholdMs) {
    Write-Host "PASS: Release detected within threshold (${ThresholdMs} ms)." -ForegroundColor Green
} else {
    Write-Warning "Release lag ${deltaMs} ms exceeds threshold ${ThresholdMs} ms."
}
