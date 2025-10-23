-- CLEAN AND CREATE LICENSE TABLES
-- This script will DROP existing tables and recreate them fresh
-- ⚠️ WARNING: This will DELETE ALL existing license data!
-- Only run this if you're okay losing any test data

-- Step 1: Drop all tables (in reverse order due to foreign keys)
DROP TABLE IF EXISTS "UserActivity" CASCADE;
DROP TABLE IF EXISTS "LicenseEvent" CASCADE;
DROP TABLE IF EXISTS "WebhookEvent" CASCADE;
DROP TABLE IF EXISTS "License" CASCADE;
DROP TABLE IF EXISTS "User" CASCADE;

-- Step 2: Drop all enums
DROP TYPE IF EXISTS "ActivityType" CASCADE;
DROP TYPE IF EXISTS "LicenseStatus" CASCADE;
DROP TYPE IF EXISTS "LicenseType" CASCADE;

-- Step 3: Create ENUMS
CREATE TYPE "LicenseType" AS ENUM ('SUBSCRIPTION', 'LIFETIME');
CREATE TYPE "LicenseStatus" AS ENUM ('ACTIVE', 'CANCELED', 'EXPIRED');
CREATE TYPE "ActivityType" AS ENUM (
  'USER_REGISTERED',
  'USER_LOGIN',
  'USER_LOGOUT',
  'CHECKOUT_STARTED',
  'CHECKOUT_COMPLETED',
  'LICENSE_ISSUED',
  'LICENSE_ACTIVATED',
  'LICENSE_DEACTIVATED',
  'LICENSE_RENEWED',
  'FEEDBACK_SUBMITTED'
);

-- Step 4: Create User table
CREATE TABLE "User" (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL
);

-- Step 5: Create License table
CREATE TABLE "License" (
  id TEXT PRIMARY KEY,
  "userId" TEXT NOT NULL,
  "licenseKey" TEXT UNIQUE NOT NULL,
  type "LicenseType" NOT NULL,
  status "LicenseStatus" NOT NULL DEFAULT 'ACTIVE',
  "stripeCustomerId" TEXT,
  "stripeSubscriptionId" TEXT UNIQUE,
  "stripePaymentIntentId" TEXT UNIQUE,
  "activatedAt" TIMESTAMP(3),
  "expiresAt" TIMESTAMP(3),
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL,
  CONSTRAINT "License_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"(id) ON DELETE CASCADE ON UPDATE CASCADE
);

-- Step 6: Create WebhookEvent table
CREATE TABLE "WebhookEvent" (
  "eventId" TEXT PRIMARY KEY,
  "seenAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Step 7: Create LicenseEvent table
CREATE TABLE "LicenseEvent" (
  id TEXT PRIMARY KEY,
  "licenseId" TEXT NOT NULL,
  type TEXT NOT NULL,
  metadata TEXT,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "LicenseEvent_licenseId_fkey" FOREIGN KEY ("licenseId") REFERENCES "License"(id) ON DELETE CASCADE ON UPDATE CASCADE
);

-- Step 8: Create UserActivity table
CREATE TABLE "UserActivity" (
  id TEXT PRIMARY KEY,
  "userId" TEXT,
  "activityType" "ActivityType" NOT NULL,
  metadata TEXT,
  "ipAddress" TEXT,
  "userAgent" TEXT,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "UserActivity_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"(id) ON DELETE SET NULL ON UPDATE CASCADE
);

-- Step 9: Create indexes
CREATE INDEX "License_userId_idx" ON "License"("userId");
CREATE INDEX "License_stripeCustomerId_idx" ON "License"("stripeCustomerId");
CREATE INDEX "LicenseEvent_licenseId_idx" ON "LicenseEvent"("licenseId");
CREATE INDEX "LicenseEvent_createdAt_idx" ON "LicenseEvent"("createdAt");
CREATE INDEX "WebhookEvent_seenAt_idx" ON "WebhookEvent"("seenAt");
CREATE INDEX "UserActivity_userId_idx" ON "UserActivity"("userId");
CREATE INDEX "UserActivity_activityType_idx" ON "UserActivity"("activityType");
CREATE INDEX "UserActivity_createdAt_idx" ON "UserActivity"("createdAt");

-- Step 10: Verify tables were created
SELECT
  tablename,
  (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = t.tablename) as column_count
FROM pg_tables t
WHERE schemaname = 'public'
  AND tablename IN ('User', 'License', 'WebhookEvent', 'LicenseEvent', 'UserActivity')
ORDER BY tablename;

-- You should see 5 rows:
-- License (12 columns)
-- LicenseEvent (5 columns)
-- User (4 columns)
-- UserActivity (7 columns)
-- WebhookEvent (2 columns)
