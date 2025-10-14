# Git History Scrubbing Execution Script
# WARNING: This script will PERMANENTLY REWRITE git history
# Ensure you have pushed commits to GitHub before running

param(
    [switch]$DryRun = $false,
    [switch]$Force = $false,
    [string]$BfgPath = "C:\ProgramData\chocolatey\lib\bfg-repo-cleaner\tools\bfg.jar"
)

Write-Host "==================================" -ForegroundColor Red
Write-Host "GIT HISTORY SCRUBBING SCRIPT" -ForegroundColor Red
Write-Host "==================================" -ForegroundColor Red
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Configuration
$projectRoot = "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
$mirrorDir = "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
$repoUrl = "https://github.com/mikha08-rgb/VoiceLite.git"
$bfgJar = $BfgPath

# Files to delete from history
$filesToDelete = @(
    "add-secrets-to-vercel.sh",
    "SECRET_ROTATION_COMPLETE.md"
)

# Secrets to verify removal
$secretToVerify = "vS89Zv4vrDNoM9zXm5aAsba"

# Safety checks
Write-Host "[Safety Check 1/3] Verifying git repository..." -ForegroundColor Green
Set-Location $projectRoot

try {
    $gitStatus = git status 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Not a git repository"
    }
    Write-Host "[OK] Git repository verified" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Not in a git repository" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[Safety Check 2/3] Verifying commits are pushed..." -ForegroundColor Green

try {
    $unpushed = git log origin/master..HEAD --oneline 2>&1
    if ($unpushed) {
        Write-Host "[WARN] You have unpushed commits:" -ForegroundColor Yellow
        Write-Host $unpushed -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Push commits before scrubbing history!" -ForegroundColor Red

        if (-not $Force) {
            exit 1
        }
        else {
            Write-Host "Continuing due to -Force flag..." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "[OK] All commits are pushed to origin" -ForegroundColor Green
    }
}
catch {
    Write-Host "[WARN] Could not verify push status" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[Safety Check 3/3] Verifying secrets exist in history..." -ForegroundColor Green

try {
    $secretExists = git log --all --full-history -p -S $secretToVerify --oneline | Select-String -Pattern $secretToVerify -Quiet
    if ($secretExists) {
        Write-Host "[WARN] Confirmed: Secrets found in git history" -ForegroundColor Yellow
    }
    else {
        Write-Host "[OK] Secrets not found - history may already be clean" -ForegroundColor Green

        if (-not $Force) {
            Write-Host "Run with -Force to proceed anyway" -ForegroundColor Yellow
            exit 0
        }
    }
}
catch {
    Write-Host "[WARN] Could not verify secret existence" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Red
Write-Host "FINAL WARNING" -ForegroundColor Red
Write-Host "==================================" -ForegroundColor Red
Write-Host ""
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  - PERMANENTLY REWRITE all git history" -ForegroundColor Yellow
Write-Host "  - BREAK all forks and pull requests" -ForegroundColor Yellow
Write-Host "  - CHANGE all commit hashes" -ForegroundColor Yellow
Write-Host "  - FORCE PUSH to GitHub" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files to be deleted from history:" -ForegroundColor Cyan
foreach ($file in $filesToDelete) {
    Write-Host "  - $file" -ForegroundColor White
}
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN - Skipping execution" -ForegroundColor Yellow
    exit 0
}

if (-not $Force) {
    $confirmation = Read-Host "Type 'DELETE HISTORY' to proceed (or Ctrl+C to cancel)"
    if ($confirmation -ne "DELETE HISTORY") {
        Write-Host "Cancelled by user" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Green
Write-Host "STEP 1: Creating Git Mirror" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

# Remove old mirror if exists
if (Test-Path $mirrorDir) {
    Write-Host "Removing old mirror directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $mirrorDir
}

Write-Host "Cloning mirror from GitHub..." -ForegroundColor Cyan
git clone --mirror $repoUrl $mirrorDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to clone mirror" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Mirror created successfully" -ForegroundColor Green
Write-Host ""

# Change to mirror directory
Set-Location $mirrorDir

Write-Host "==================================" -ForegroundColor Green
Write-Host "STEP 2: Running BFG Repo-Cleaner" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

foreach ($file in $filesToDelete) {
    Write-Host "Deleting '$file' from history..." -ForegroundColor Cyan

    java -jar $bfgJar --delete-files $file .

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] BFG failed for file: $file" -ForegroundColor Red
        exit 1
    }

    Write-Host "[OK] File deleted from history: $file" -ForegroundColor Green
    Write-Host ""
}

Write-Host "==================================" -ForegroundColor Green
Write-Host "STEP 3: Cleaning Git References" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

Write-Host "Expiring reflog..." -ForegroundColor Cyan
git reflog expire --expire=now --all

Write-Host "Running aggressive garbage collection..." -ForegroundColor Cyan
git gc --prune=now --aggressive

Write-Host "[OK] Git references cleaned" -ForegroundColor Green
Write-Host ""

Write-Host "==================================" -ForegroundColor Green
Write-Host "STEP 4: Verifying Removal" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

$stillExists = git log --all --full-history -p -S $secretToVerify --oneline 2>&1

if ($stillExists) {
    Write-Host "[ERROR] CRITICAL: Secrets still found in history!" -ForegroundColor Red
    Write-Host "Do not proceed with force push!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Found in commits:" -ForegroundColor Yellow
    Write-Host $stillExists -ForegroundColor Yellow
    exit 1
}
else {
    Write-Host "[OK] Verification passed - secrets removed from history" -ForegroundColor Green
}

Write-Host ""

Write-Host "==================================" -ForegroundColor Green
Write-Host "STEP 5: Force Push to GitHub" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

Write-Host "Pushing all branches..." -ForegroundColor Cyan
git push --force --all

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to push branches" -ForegroundColor Red
    exit 1
}

Write-Host "Pushing tags..." -ForegroundColor Cyan
git push --force --tags

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to push tags" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Force push completed" -ForegroundColor Green
Write-Host ""

Write-Host "==================================" -ForegroundColor Green
Write-Host "[SUCCESS] GIT HISTORY SCRUBBING COMPLETE!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Update your local repository:" -ForegroundColor White
Write-Host "   cd '$projectRoot'" -ForegroundColor Cyan
Write-Host "   git fetch origin" -ForegroundColor Cyan
Write-Host "   git reset --hard origin/master" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Verify on GitHub:" -ForegroundColor White
Write-Host "   https://github.com/mikha08-rgb/VoiceLite/commits/master" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. Proceed to Phase 1 Step 1.4: Generate new Ed25519 keypairs" -ForegroundColor White
Write-Host ""

# Return to project directory
Set-Location $projectRoot
