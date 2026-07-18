#!/usr/bin/env bash
# Mint 4 lifetime gratis license keys tied to a single email.
# Usage: bash scripts/mint-gratis-keys.sh <email>
# Requires: .env.local with ADMIN_SECRET_TOKEN, run from voicelite-web/.

set -u

if [ $# -lt 1 ]; then
  echo "Usage: bash scripts/mint-gratis-keys.sh <email>"
  exit 1
fi

EMAIL="$1"
DATE_TAG=$(date +%Y-%m-%d)

if [ ! -f .env.local ]; then
  echo "ERROR: .env.local not found. Run 'npx vercel env pull .env.local' first." >&2
  exit 1
fi

TOKEN=$(grep ^ADMIN_SECRET_TOKEN .env.local | cut -d= -f2- | tr -d '"' | tr -d "'")

if [ -z "$TOKEN" ]; then
  echo "ERROR: ADMIN_SECRET_TOKEN not found in .env.local." >&2
  exit 1
fi

echo "Minting 4 lifetime keys for $EMAIL"
echo

SUCCESS_COUNT=0
UNDELIVERED_KEYS=""
for i in 1 2 3 4; do
  echo "=== KEY $i ==="
  RESPONSE=$(curl -s -X POST https://voicelite.app/api/admin/process-payment \
    -H "x-admin-token: $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$EMAIL\",\"paymentIntentId\":\"gratis_gphealth_team_${i}_${DATE_TAG}\",\"customerId\":\"manual_gratis_gphealth\"}")
  echo "$RESPONSE"
  echo

  # The endpoint returns "success":true even when the license was minted but the
  # email FAILED to send (email.sent is false in that case). Count success only
  # when the key was minted AND the email actually went out.
  if echo "$RESPONSE" | grep -q '"success":true'; then
    if echo "$RESPONSE" | grep -q '"sent":true'; then
      SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    else
      KEY=$(echo "$RESPONSE" | grep -o '"licenseKey":"[^"]*"' | head -1 | cut -d'"' -f4)
      UNDELIVERED_KEYS="$UNDELIVERED_KEYS ${KEY:-unknown-key-$i}"
    fi
  fi
done

echo "==========================================="
echo "Result: $SUCCESS_COUNT of 4 keys minted AND emailed successfully."
echo "==========================================="

if [ -n "$UNDELIVERED_KEYS" ]; then
  echo "" >&2
  echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" >&2
  echo "WARNING: these keys were MINTED but their email FAILED to send:" >&2
  for KEY in $UNDELIVERED_KEYS; do
    echo "  - $KEY" >&2
  done
  echo "The licenses exist in the DB but $EMAIL has NOT received them." >&2
  echo "Resend manually, e.g.:" >&2
  echo "  curl -X POST https://voicelite.app/api/admin/get-license \\" >&2
  echo "    -H \"x-admin-token: \$TOKEN\" -H 'Content-Type: application/json' \\" >&2
  echo "    -d '{\"email\":\"$EMAIL\",\"sendEmail\":true}'" >&2
  echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" >&2
fi

if [ "$SUCCESS_COUNT" -ne 4 ]; then
  exit 1
fi
