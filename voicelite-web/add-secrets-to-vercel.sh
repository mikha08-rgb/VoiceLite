#!/bin/bash
# Automated Vercel Secret Deployment Script
# Run this to add all rotated secrets to Vercel production

set -e  # Exit on error

echo "🔐 VoiceLite Secret Rotation - Vercel Deployment"
echo "================================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# New secrets (generated 2025-10-03)
LICENSE_SIGNING_PRIVATE="vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k"
LICENSE_SIGNING_PUBLIC="fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc"
CRL_SIGNING_PRIVATE="qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE"
CRL_SIGNING_PUBLIC="19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M"
MIGRATION_SECRET="443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210"

echo "📋 This script will add 5 environment variables to Vercel production:"
echo "   1. LICENSE_SIGNING_PRIVATE_B64"
echo "   2. LICENSE_SIGNING_PUBLIC_B64"
echo "   3. CRL_SIGNING_PRIVATE_B64"
echo "   4. CRL_SIGNING_PUBLIC_B64"
echo "   5. MIGRATION_SECRET"
echo ""
echo -e "${YELLOW}⚠️  WARNING: This will OVERWRITE existing values if they exist!${NC}"
echo ""
read -p "Continue? (yes/no): " confirm

if [ "$confirm" != "yes" ]; then
    echo "❌ Aborted."
    exit 1
fi

echo ""
echo "🚀 Adding secrets to Vercel..."
echo ""

# Add each secret
echo "1/5 Adding LICENSE_SIGNING_PRIVATE_B64..."
echo "$LICENSE_SIGNING_PRIVATE" | vercel env add LICENSE_SIGNING_PRIVATE_B64 production --force

echo "2/5 Adding LICENSE_SIGNING_PUBLIC_B64..."
echo "$LICENSE_SIGNING_PUBLIC" | vercel env add LICENSE_SIGNING_PUBLIC_B64 production --force

echo "3/5 Adding CRL_SIGNING_PRIVATE_B64..."
echo "$CRL_SIGNING_PRIVATE" | vercel env add CRL_SIGNING_PRIVATE_B64 production --force

echo "4/5 Adding CRL_SIGNING_PUBLIC_B64..."
echo "$CRL_SIGNING_PUBLIC" | vercel env add CRL_SIGNING_PUBLIC_B64 production --force

echo "5/5 Adding MIGRATION_SECRET..."
echo "$MIGRATION_SECRET" | vercel env add MIGRATION_SECRET production --force

echo ""
echo -e "${GREEN}✅ All secrets added successfully!${NC}"
echo ""
echo "📝 Next steps:"
echo "   1. Deploy to production: vercel deploy --prod"
echo "   2. Update desktop app public key in LicenseService.cs"
echo "   3. Configure Resend API key (CRITICAL - email broken without this)"
echo ""
