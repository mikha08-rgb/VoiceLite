# PowerShell script to run tests with coverage

Write-Host "Running VoiceLite Tests with Coverage..." -ForegroundColor Green

# Clean previous results
if (Test-Path "TestResults") {
    Remove-Item -Path "TestResults" -Recurse -Force
}

# Run tests with coverage
dotnet test VoiceLite.Tests\VoiceLite.Tests.csproj `
    --configuration Release `
    --logger "console;verbosity=normal" `
    --logger "html;logfilename=testresults.html" `
    --collect:"XPlat Code Coverage" `
    --settings VoiceLite.Tests\coverlet.runsettings `
    --results-directory TestResults

# Check if reportgenerator is installed
$reportGenInstalled = dotnet tool list -g | Select-String "reportgenerator"

if (-not $reportGenInstalled) {
    Write-Host "Installing ReportGenerator tool..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
}

# Generate HTML coverage report
$coverageFile = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1

if ($coverageFile) {
    Write-Host "Generating coverage report..." -ForegroundColor Green

    reportgenerator `
        -reports:"$($coverageFile.FullName)" `
        -targetdir:"TestResults\CoverageReport" `
        -reporttypes:"Html;Badges;TextSummary" `
        -historydir:"TestResults\CoverageHistory" `
        -title:"VoiceLite Test Coverage" `
        -verbosity:"Warning"

    # Display summary
    if (Test-Path "TestResults\CoverageReport\Summary.txt") {
        Write-Host "`nCoverage Summary:" -ForegroundColor Cyan
        Get-Content "TestResults\CoverageReport\Summary.txt"
    }

    Write-Host "`nCoverage report generated at: TestResults\CoverageReport\index.html" -ForegroundColor Green

    # Open report in browser
    $openReport = Read-Host "Open coverage report in browser? (Y/N)"
    if ($openReport -eq 'Y' -or $openReport -eq 'y') {
        Start-Process "TestResults\CoverageReport\index.html"
    }
} else {
    Write-Host "No coverage file found!" -ForegroundColor Red
}