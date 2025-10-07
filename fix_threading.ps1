# Fix threading bug in SaveSettingsInternalAsync
$file = "VoiceLite/VoiceLite/MainWindow.xaml.cs"
$content = Get-Content $file -Raw

# Pattern to find and replace
$pattern = @'
        private async Task SaveSettingsInternalAsync\(\)
        \{
            // TIER 1\.4: Use SemaphoreSlim instead of lock for async compatibility
            await saveSettingsSemaphore\.WaitAsync\(\);
            try
            \{
                try
                \{
                    // Ensure AppData directory exists
                    EnsureAppDataDirectoryExists\(\);

                    settings\.MinimizeToTray = MinimizeCheckBox\.IsChecked == true;
'@

$replacement = @'
        private async Task SaveSettingsInternalAsync()
        {
            // v1.0.55 BUG FIX: Capture UI state on UI thread BEFORE going async
            // Prevents "The calling thread cannot access this object" errors
            bool minimizeToTray = false;
            await Dispatcher.InvokeAsync(() =>
            {
                minimizeToTray = MinimizeCheckBox.IsChecked == true;
            });

            // TIER 1.4: Use SemaphoreSlim instead of lock for async compatibility
            await saveSettingsSemaphore.WaitAsync();
            try
            {
                try
                {
                    // Ensure AppData directory exists
                    EnsureAppDataDirectoryExists();

                    settings.MinimizeToTray = minimizeToTray;
'@

$content = $content -replace $pattern, $replacement
Set-Content $file -Value $content -NoNewline
Write-Host "Fix applied successfully!"
