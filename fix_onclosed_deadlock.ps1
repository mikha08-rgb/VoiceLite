# v1.0.56 Fix: Replace blocking .Wait() with async OnClosing pattern
$file = "VoiceLite/VoiceLite/MainWindow.xaml.cs"
$content = Get-Content $file -Raw

# Find and replace the OnClosed method
$pattern = @'
        protected override void OnClosed\(EventArgs e\)
        \{
            // CRITICAL FIX \(BUG-002\): Flush pending settings save BEFORE disposal
            // If timer is active, debounce hasn't fired yet - force immediate save to prevent data loss
            if \(settingsSaveTimer != null && settingsSaveTimer\.IsEnabled\)
            \{
                settingsSaveTimer\.Stop\(\);
                SaveSettingsInternalAsync\(\)\.Wait\(\); // TIER 1\.4: Wait for async save to complete before shutdown
                ErrorLogger\.LogMessage\("BUG-002 FIX: Flushed pending settings save on app close"\);
            \}

            SaveSettings\(\); // Belt-and-suspenders: call debounced save too \(will be no-op if timer null\)
'@

$replacement = @'
        // v1.0.56 CRITICAL FIX: Prevent deadlock on app close
        // Old approach: SaveSettingsInternalAsync().Wait() blocks UI thread for 5-30 seconds
        // New approach: Use async OnClosing to await settings save without blocking
        private bool isClosingHandled = false;

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!isClosingHandled)
            {
                e.Cancel = true; // Prevent immediate close
                isClosingHandled = true;

                // CRITICAL FIX (BUG-002 + v1.0.56): Flush pending settings save BEFORE disposal
                // NOW ASYNC - no UI thread blocking!
                if (settingsSaveTimer != null && settingsSaveTimer.IsEnabled)
                {
                    settingsSaveTimer.Stop();
                    await SaveSettingsInternalAsync(); // NO .Wait() - async all the way
                    ErrorLogger.LogMessage("v1.0.56 FIX: Flushed pending settings save on app close (async, no blocking)");
                }

                SaveSettings(); // Belt-and-suspenders: call debounced save too (will be no-op if timer null)

                base.OnClosing(e);
                Close(); // Now actually close the window
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Settings save already handled in OnClosing (async pattern)
'@

$content = $content -replace $pattern, $replacement
Set-Content $file -Value $content -NoNewline
Write-Host "Fixed OnClosed deadlock!"
