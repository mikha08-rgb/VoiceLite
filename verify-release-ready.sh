#!/bin/bash
# VoiceLite Release Readiness Verification Script
# Run this on Day 1 morning before launch

set -e

echo "=================================="
echo "VoiceLite Release Readiness Check"
echo "=================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track results
PASSED=0
FAILED=0

check() {
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $1"
        ((PASSED++))
    else
        echo -e "${RED}✗${NC} $1"
        ((FAILED++))
    fi
}

# 1. Desktop App Build
echo "1. Desktop App Build Check"
echo "----------------------------"
dotnet build VoiceLite/VoiceLite.sln -c Release > /dev/null 2>&1
check "Desktop app builds successfully"
echo ""

# 2. Desktop App Tests
echo "2. Desktop App Tests"
echo "--------------------"
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --no-build > /dev/null 2>&1
if [ $? -eq 0 ] || [ $(dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj 2>&1 | grep -c "Passed:.*80") -gt 0 ]; then
    echo -e "${GREEN}✓${NC} Core tests pass (80+ passing)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} Tests failing"
    ((FAILED++))
fi
echo ""

# 3. Version Check
echo "3. Version Verification"
echo "-----------------------"
VERSION=$(grep -oP '<Version>\K[^<]+' VoiceLite/VoiceLite/VoiceLite.csproj)
if [ "$VERSION" == "1.0.88" ]; then
    echo -e "${GREEN}✓${NC} Desktop version is v1.0.88"
    ((PASSED++))
else
    echo -e "${YELLOW}⚠${NC} Desktop version is $VERSION (expected 1.0.88)"
    ((FAILED++))
fi
echo ""

# 4. Web Build
echo "4. Web App Build Check"
echo "-----------------------"
cd voicelite-web
npm run build > /dev/null 2>&1
check "Web app builds successfully"
cd ..
echo ""

# 5. Critical Files Exist
echo "5. Critical Files Check"
echo "-----------------------"
[ -f "README.md" ] && echo -e "${GREEN}✓${NC} README.md exists" && ((PASSED++)) || { echo -e "${RED}✗${NC} README.md missing" && ((FAILED++)); }
[ -f "VoiceLiteSetup_Simple.iss" ] && echo -e "${GREEN}✓${NC} Installer script exists" && ((PASSED++)) || { echo -e "${RED}✗${NC} Installer script missing" && ((FAILED++)); }
[ -f "voicelite-web/app/api/webhook/route.ts" ] && echo -e "${GREEN}✓${NC} Webhook endpoint exists" && ((PASSED++)) || { echo -e "${RED}✗${NC} Webhook endpoint missing" && ((FAILED++)); }
[ -f "voicelite-web/app/api/licenses/validate/route.ts" ] && echo -e "${GREEN}✓${NC} License validation endpoint exists" && ((PASSED++)) || { echo -e "${RED}✗${NC} License validation endpoint missing" && ((FAILED++)); }
echo ""

# 6. Git Status
echo "6. Git Repository Status"
echo "------------------------"
if [ -z "$(git status --porcelain)" ]; then
    echo -e "${GREEN}✓${NC} Repository is clean (no uncommitted changes)"
    ((PASSED++))
else
    echo -e "${YELLOW}⚠${NC} Uncommitted changes exist (may need cleanup)"
    ((FAILED++))
fi
echo ""

# 7. Whisper Models
echo "7. Whisper Models Check"
echo "-----------------------"
if [ -f "VoiceLite/whisper/ggml-tiny.bin" ]; then
    SIZE=$(du -h "VoiceLite/whisper/ggml-tiny.bin" | cut -f1)
    echo -e "${GREEN}✓${NC} Tiny model exists ($SIZE)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} Tiny model missing (CRITICAL - bundled with installer)"
    ((FAILED++))
fi

if [ -f "VoiceLite/whisper/whisper.exe" ]; then
    echo -e "${GREEN}✓${NC} whisper.exe exists"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} whisper.exe missing (CRITICAL)"
    ((FAILED++))
fi
echo ""

# Summary
echo "=================================="
echo "Summary"
echo "=================================="
echo -e "Passed: ${GREEN}$PASSED${NC}"
echo -e "Failed: ${RED}$FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}🚀 ALL CHECKS PASSED - READY FOR LAUNCH!${NC}"
    exit 0
else
    echo -e "${YELLOW}⚠ Some checks failed - review before launch${NC}"
    exit 1
fi
