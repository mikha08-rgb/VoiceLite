$ErrorActionPreference = "Stop"

try {
    Write-Host "Fetching latest ConfuserEx release information..."
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/mkaring/ConfuserEx/releases/latest"

    $cliAsset = $response.assets | Where-Object { $_.name -like "*CLI.zip" }

    if ($cliAsset) {
        Write-Host "Found CLI asset: $($cliAsset.name)"
        Write-Host "Download URL: $($cliAsset.browser_download_url)"
        Write-Host "Size: $([math]::Round($cliAsset.size / 1MB, 2)) MB"

        $outputPath = "VoiceLite\Tools\ConfuserEx\ConfuserEx-CLI.zip"
        Write-Host "Downloading to: $outputPath"

        Invoke-WebRequest -Uri $cliAsset.browser_download_url -OutFile $outputPath
        Write-Host "Download complete!"

        # Extract the ZIP file
        Write-Host "Extracting ConfuserEx CLI..."
        Expand-Archive -Path $outputPath -DestinationPath "VoiceLite\Tools\ConfuserEx" -Force

        Write-Host "ConfuserEx CLI setup complete!"

        # List the extracted files
        Get-ChildItem "VoiceLite\Tools\ConfuserEx" -Recurse | Select-Object Name
    } else {
        Write-Host "ERROR: CLI asset not found in latest release"
        Write-Host "Available assets:"
        $response.assets | ForEach-Object { Write-Host " - $($_.name)" }
    }
} catch {
    Write-Host "ERROR: Failed to download ConfuserEx"
    Write-Host $_.Exception.Message
}