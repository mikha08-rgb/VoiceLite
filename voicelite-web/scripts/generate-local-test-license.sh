#!/bin/bash

# Quick script to generate a test license for local development
# Usage: ./scripts/generate-local-test-license.sh [email]

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Default email
EMAIL="${1:-test@example.com}"

# Check if .env.local exists and has ADMIN_SECRET
if [ ! -f .env.local ]; then
    echo -e "${RED}❌ Error: .env.local not found${NC}"
    echo "Create .env.local and add: ADMIN_SECRET=your-secret-here"
    exit 1
fi

if ! grep -q "ADMIN_SECRET" .env.local; then
    echo -e "${RED}❌ Error: ADMIN_SECRET not found in .env.local${NC}"
    echo "Add to .env.local: ADMIN_SECRET=your-secret-here"
    exit 1
fi

# Extract ADMIN_SECRET from .env.local
ADMIN_SECRET=$(grep ADMIN_SECRET .env.local | cut -d '=' -f2 | tr -d '"' | tr -d "'")

if [ -z "$ADMIN_SECRET" ]; then
    echo -e "${RED}❌ Error: ADMIN_SECRET is empty${NC}"
    exit 1
fi

echo -e "${YELLOW}🔑 Generating test license for: ${EMAIL}${NC}"
echo ""

# Make the API call
RESPONSE=$(curl -s -X POST http://localhost:3000/api/admin/generate-test-license \
  -H "x-admin-secret: $ADMIN_SECRET" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$EMAIL\"}")

# Check if successful
if echo "$RESPONSE" | grep -q '"success":true'; then
    LICENSE_KEY=$(echo "$RESPONSE" | grep -o '"key":"[^"]*"' | cut -d'"' -f4)
    echo -e "${GREEN}✅ License generated successfully!${NC}"
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${GREEN}License Key: ${LICENSE_KEY}${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    echo "📋 Next steps:"
    echo "1. Copy the license key above"
    echo "2. Launch VoiceLite desktop app"
    echo "3. Click 'Activate License'"
    echo "4. Paste the key and activate"
    echo ""
else
    echo -e "${RED}❌ Failed to generate license${NC}"
    echo ""
    echo "Response:"
    echo "$RESPONSE" | jq . 2>/dev/null || echo "$RESPONSE"
    echo ""
    echo "Make sure:"
    echo "1. Next.js dev server is running (npm run dev)"
    echo "2. NODE_ENV is not set to 'production'"
    echo "3. ADMIN_SECRET is correctly configured"
    exit 1
fi
