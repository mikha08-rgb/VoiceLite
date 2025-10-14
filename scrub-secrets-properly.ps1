# Proper Git History Scrubbing - Replace Secrets with REDACTED
# This version uses BFG's --replace-text to redact secrets

param(
    [string]$BfgPath = "$env:USERPROFILE\Downloads\bfg.jar"
)

Write-Host "==================================" -ForegroundColor Red
Write-Host "GIT SECRETS SCRUBBING (PROPER)" -ForegroundColor Red
Write-Host "==================================" -ForegroundColor Red
Write-Host ""

$projectRoot = "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
$mirrorDir = "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
$repoUrl = "https://github.com/mikha08-rgb/VoiceLite.git"
$secretsFile = "$projectRoot\secrets-to-redact.txt"

Set-Location $projectRoot

# Step 1: Remove old mirror
if (Test-Path $mirrorDir) {
    Write-Host "[Step 1] Removing old mirror..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $mirrorDir
    Write-Host "[OK] Old mirror removed" -ForegroundColor Green
}

# Step 2: Clone fresh mirror
Write-Host "[Step 2] Cloning fresh mirror from GitHub..." -ForegroundColor Cyan
git clone --mirror $repoUrl $mirrorDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to clone mirror" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Mirror cloned" -ForegroundColor Green
Write-Host ""

# Step 3: Run BFG to replace secrets
Write-Host "[Step 3] Running BFG to redact secrets..." -ForegroundColor Cyan
Set-Location $mirrorDir

java -jar $BfgPath --replace-text $secretsFile .

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] BFG failed" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Secrets redacted" -ForegroundColor Green
Write-Host ""

# Step 4: Clean refs
Write-Host "[Step 4] Cleaning git references..." -ForegroundColor Cyan
git reflog expire --expire=now --all
git gc --prune=now --aggressive
Write-Host "[OK] References cleaned" -ForegroundColor Green
Write-Host ""

# Step 5: Verify
Write-Host "[Step 5] Verifying secrets removed..." -ForegroundColor Cyan
$stillExists = git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba" --oneline 2>&1

if ($stillExists) {
    Write-Host "[ERROR] Secrets still found in history!" -ForegroundColor Red
    Write-Host "Found in:" -ForegroundColor Yellow
    Write-Host $stillExists -ForegroundColor Yellow
    Write-Host ""
    Write-Host "DO NOT force push!" -ForegroundColor Red
    exit 1
}
else {
    Write-Host "[OK] Verification passed - secrets removed!" -ForegroundColor Green
}
Write-Host ""

# Step 6: Force push
Write-Host "[Step 6] Force pushing to GitHub..." -ForegroundColor Red
$confirmation = Read-Host "Type 'PUSH NOW' to force push (or Ctrl+C to cancel)"

if ($confirmation -ne "PUSH NOW") {
    Write-Host "Cancelled by user" -ForegroundColor Yellow
    exit 0
}

git push --force --all

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Force push failed" -ForegroundColor Red
    exit 1
}

git push --force --tags

Write-Host "[OK] Force push completed!" -ForegroundColor Green
Write-Host ""

# Step 7: Update local repo
Write-Host "[Step 7] Updating your local repository..." -ForegroundColor Cyan
Set-Location $projectRoot
git fetch origin
git reset --hard origin/master

Write-Host ""
Write-Host "==================================" -ForegroundColor Green
Write-Host "[SUCCESS] SCRUBBING COMPLETE!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""
Write-Host "Verification:" -ForegroundColor Yellow
Write-Host "1. Search GitHub: https://github.com/mikha08-rgb/VoiceLite/search?q=vS89Zv4vrDNoM9zXm5aAsba" -ForegroundColor Cyan
Write-Host "   Should show: NO RESULTS" -ForegroundColor White
Write-Host ""
Write-Host "2. Search locally:" -ForegroundColor Yellow
Write-Host '   git log --all -p -S "vS89Zv4vrDNoM9zXm5aAsba"' -ForegroundColor Cyan
Write-Host "   Should show: NOTHING" -ForegroundColor White
Write-Host ""
Write-Host "Next: Generate new Ed25519 keypairs" -ForegroundColor Yellow
