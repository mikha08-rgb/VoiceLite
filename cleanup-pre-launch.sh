#!/bin/bash
# Pre-Launch Cleanup Script
# Removes debugging files and test scripts before public release

echo "=================================="
echo "VoiceLite Pre-Launch Cleanup"
echo "=================================="
echo ""

# Backup first (just in case)
echo "Creating backup of debugging files..."
mkdir -p .archive/pre-launch-backup
cp *.md .archive/pre-launch-backup/ 2>/dev/null || true
cp *.js .archive/pre-launch-backup/ 2>/dev/null || true
cp *.sh .archive/pre-launch-backup/ 2>/dev/null || true
echo "✓ Backup created in .archive/pre-launch-backup/"
echo ""

# Remove debugging markdown files
echo "Removing debugging documentation..."
rm -f ACTUAL_FIX_NEEDED.md
rm -f DEBUGGING_STEPS.md
rm -f DEPLOYMENT_FIX.md
rm -f DEPLOYMENT_SUCCESS.md
rm -f EMAIL_FIX_SUMMARY.md
rm -f FINAL_DIAGNOSIS.md
rm -f FINAL_SOLUTION.md
rm -f FINAL_VERIFICATION.md
rm -f FIX_DATABASE.md
rm -f FIX_INSTRUCTIONS.md
rm -f ISSUE_RESOLVED.md
rm -f THE_ACTUAL_PROBLEM.md
rm -f VERIFICATION_INSTRUCTIONS.md
rm -f WEBHOOK_DEBUG_GUIDE.md
echo "✓ Debugging documentation removed"
echo ""

# Remove temporary test scripts
echo "Removing temporary scripts..."
rm -f add-vercel-envs.sh
rm -f apply-migration.js
rm -f check-database-licenses.js
rm -f check-database.js
rm -f check-email-events.js
rm -f check-latest-events.js
rm -f check-latest-payment-now.js
rm -f check-latest-payment.js
rm -f check-resend-logs.js
rm -f check-session-details.js
rm -f check-stripe-recent-events.js
rm -f check-stripe-webhook-attempts.js
rm -f check-stripe-webhook.js
rm -f check-webhook-delivery.js
rm -f check-webhook-endpoints.js
rm -f create-all-licenses-now.js
rm -f create-license-for-mikhail.js
rm -f diagnose-webhook-error.js
rm -f fix-ga-env.txt
rm -f manual-send-email.js
rm -f manually-send-license-email.js
rm -f resend-license.js
rm -f run-migrations.js
rm -f send-emails-for-existing-payments.js
rm -f send-my-license.js
rm -f simulate-payment-test.js
rm -f simulate-webhook-call.js
rm -f test-complete-flow.js
rm -f test-email-send.js
rm -f test-live-checkout.js
rm -f test-live-payment-flow.js
rm -f test-resend-directly.js
rm -f test-resend.js
rm -f test-webhook-directly.js
rm -f trigger-webhook-for-recent-payment.js
rm -f verify-live-deployment.js
rm -f verify-webhook-fix.js
echo "✓ Temporary scripts removed"
echo ""

# Remove root node_modules and package files (only in voicelite-web/)
echo "Removing root package files..."
rm -f package.json
rm -f package-lock.json
rm -rf node_modules/
echo "✓ Root package files removed"
echo ""

# Keep important documentation
echo "Keeping essential files:"
echo "  - README.md (project landing page)"
echo "  - CLAUDE.md (development guide)"
echo "  - CONTRIBUTING.md (contributor guide)"
echo "  - SECURITY.md (security policy)"
echo "  - FIX_EMAIL_DELIVERY.md (historical reference)"
echo "  - LEAN_STARTUP_RELEASE_PLAN.md (this release plan)"
echo ""

# Git status
echo "Current git status:"
git status --short
echo ""

echo "=================================="
echo "Cleanup Complete!"
echo "=================================="
echo ""
echo "Next steps:"
echo "1. Review git status above"
echo "2. Commit changes: git add . && git commit -m 'chore: clean up debugging files for v1.0.88 release'"
echo "3. Run verification script: ./verify-release-ready.sh"
echo "4. Tag release: git tag v1.0.88 && git push --tags"
echo ""
