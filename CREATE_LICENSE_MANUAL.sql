-- Manual License Creation for VoiceLite Pro
-- Run this in Supabase SQL Editor if webhook is failing

-- STEP 1: Replace these values
-- YOUR_EMAIL: The email you used for Stripe purchase
-- YOUR_LICENSE_KEY: Generate at https://www.uuidgenerator.net/

-- Create user (if doesn't exist)
INSERT INTO "User" (id, email, "createdAt", "updatedAt")
VALUES (
  gen_random_uuid(),
  'YOUR_EMAIL_HERE',  -- ← CHANGE THIS
  NOW(),
  NOW()
)
ON CONFLICT (email) DO NOTHING;

-- Create license
WITH user_id AS (
  SELECT id FROM "User" WHERE email = 'YOUR_EMAIL_HERE'  -- ← CHANGE THIS
)
INSERT INTO "License" (
  id,
  "userId",
  "licenseKey",
  type,
  status,
  "stripePaymentIntentId",
  "activatedAt",
  "createdAt",
  "updatedAt"
)
SELECT
  gen_random_uuid(),
  id,
  'YOUR_LICENSE_KEY_HERE',  -- ← CHANGE THIS (UUID format)
  'LIFETIME',
  'ACTIVE',
  'pi_manual_' || floor(random() * 1000000)::text,
  NOW(),
  NOW(),
  NOW()
FROM user_id
RETURNING "licenseKey", "createdAt";

-- Verify it was created
SELECT l."licenseKey", l.type, l.status, l."createdAt", u.email
FROM "License" l
JOIN "User" u ON l."userId" = u.id
WHERE u.email = 'YOUR_EMAIL_HERE'  -- ← CHANGE THIS
ORDER BY l."createdAt" DESC
LIMIT 1;

-- INSTRUCTIONS:
-- 1. Go to https://supabase.com → Your project → SQL Editor
-- 2. Replace YOUR_EMAIL_HERE with: mikhail.lev08@gmail.com (or whatever email you used)
-- 3. Replace YOUR_LICENSE_KEY_HERE with a UUID from https://www.uuidgenerator.net/
-- 4. Click "Run"
-- 5. Copy the license key from the output
-- 6. Test in VoiceLite desktop app: Settings → License → Paste → Activate
