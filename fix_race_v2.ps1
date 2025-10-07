# v1.0.57 Fix: Add lock to StartRecording method
$file = "VoiceLite/VoiceLite/MainWindow.xaml.cs"
$content = Get-Content $file

# Insert lock opening after line 963 (after the entry log)
$newLines = @()
for ($i = 0; $i -lt $content.Count; $i++) {
    $newLines += $content[$i]

    # After "ErrorLogger.LogDebug($"StartRecording: Entry..." (line 963)
    if ($i -eq 962 -and $content[$i] -match 'StartRecording: Entry') {
        $newLines += ''
        $newLines += '            // v1.0.57 CRITICAL FIX: Wrap entire method in lock to prevent race condition'
        $newLines += '            // BUG: 3 simultaneous StartRecording() calls were bypassing all guards (seen in logs)'
        $newLines += '            // Root cause: Guards checked state outside lock, so all 3 threads saw IsRecording=false'
        $newLines += '            lock (recordingLock)'
        $newLines += '            {'
    }

    # Before final closing brace of StartRecording (line 1029)
    if ($i -eq 1027 -and $content[$i] -match 'Failed to start recording') {
        # Next line will be the closing brace for else block, then method closing brace
        # We need to add our lock closing brace before the method closing brace
    }
}

# Now insert closing brace before line 1029 (method closing brace)
$finalLines = @()
for ($i = 0; $i -lt $newLines.Count; $i++) {
    if ($i -gt 0 -and $newLines[$i] -match '^\s*\}$' -and $newLines[$i-1] -match 'UpdateStatus.*Failed to start recording') {
        $finalLines += $newLines[$i] # closing brace for else block
        $finalLines += '            } // v1.0.57: Close lock scope'
    }
    else {
        $finalLines += $newLines[$i]
    }
}

$finalLines | Set-Content $file
Write-Host "Fixed StartRecording() race condition with lock!"
