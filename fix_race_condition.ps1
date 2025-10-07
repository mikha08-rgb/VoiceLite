# v1.0.57 Fix: Prevent StartRecording() race condition
$file = "VoiceLite/VoiceLite/MainWindow.xaml.cs"
$content = Get-Content $file -Raw

# Pattern to find StartRecording method
$pattern = @'
        private void StartRecording\(\)
        \{
            ErrorLogger\.LogDebug\(\$"StartRecording: Entry - // WEEK1-DAY3: State managed by coordinator - IsRecording =\{IsRecording\}"\);

            // CRITICAL FIX: Validate AudioRecorder is not already recording \(prevents race conditions\)
            var recorder = audioRecorder;
'@

$replacement = @'
        private void StartRecording()
        {
            ErrorLogger.LogDebug($"StartRecording: Entry - // WEEK1-DAY3: State managed by coordinator - IsRecording ={IsRecording}");

            // v1.0.57 CRITICAL FIX: Wrap entire method in lock to prevent race condition
            // BUG: 3 simultaneous StartRecording() calls were bypassing all guards (seen in logs)
            // Root cause: Guards checked state outside lock, so all 3 threads saw IsRecording=false
            lock (recordingLock)
            {
                // CRITICAL FIX: Validate AudioRecorder is not already recording (prevents race conditions)
                var recorder = audioRecorder;
'@

if ($content -match [regex]::Escape($pattern))
{
    $content = $content -replace [regex]::Escape($pattern), $replacement

    # Now we need to close the lock at the end of StartRecording
    # Find the closing brace of recordingElapsedTimer creation block and add closing brace there
    $pattern2 = @'
            else
            \{
                // Recording failed to start - rollback state
                ErrorLogger\.LogWarning\("StartRecording: Recording failed to start, rolling back state"\);
                // WEEK1-DAY3: State managed by coordinator - // WEEK1-DAY3: State managed by coordinator - IsRecording = false;
                UpdateStatus\("Failed to start recording", Brushes\.Red\);
            \}
        \}
'@

    $replacement2 = @'
            else
            {
                // Recording failed to start - rollback state
                ErrorLogger.LogWarning("StartRecording: Recording failed to start, rolling back state");
                // WEEK1-DAY3: State managed by coordinator - // WEEK1-DAY3: State managed by coordinator - IsRecording = false;
                UpdateStatus("Failed to start recording", Brushes.Red);
            }
            } // v1.0.57: Close lock scope
        }
'@

    $content = $content -replace [regex]::Escape($pattern2), $replacement2
    Set-Content $file -Value $content -NoNewline
    Write-Host "Fixed StartRecording() race condition!"
}
else
{
    Write-Host "Pattern not found - file may have changed"
}
