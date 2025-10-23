-- MINIMAL LICENSE SYSTEM - Only tables needed for Stripe webhook to work
-- Based on actual Prisma schema at voicelite-web/prisma/schema.prisma
-- Run this in Supabase SQL Editor

-- Step 1: Create ENUMS
CREATE TYPE "LicenseType" AS ENUM ('SUBSCRIPTION', 'LIFETIME');
CREATE TYPE "LicenseStatus" AS ENUM ('ACTIVE', 'CANCELED', 'EXPIRED');

-- Step 2: Create User table (required for foreign keys)
CREATE TABLE "User" (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "updatedAt" TIMESTAMP(3) NOT NULL
);

-- Step 3: Create License table (main table for licenses)
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

-- Step 4: Create WebhookEvent table (prevents duplicate processing)
CREATE TABLE "WebhookEvent" (
  "eventId" TEXT PRIMARY KEY,
  "seenAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Step 5: Create LicenseEvent table (audit log for licenses)
CREATE TABLE "LicenseEvent" (
  id TEXT PRIMARY KEY,
  "licenseId" TEXT NOT NULL,
  type TEXT NOT NULL,
  metadata TEXT,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "LicenseEvent_licenseId_fkey" FOREIGN KEY ("licenseId") REFERENCES "License"(id) ON DELETE CASCADE ON UPDATE CASCADE
);

-- Step 6: Create UserActivity table (tracks user actions like LICENSE_ISSUED)
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

-- Step 7: Create indexes for performance
CREATE INDEX "License_userId_idx" ON "License"("userId");
CREATE INDEX "License_stripeCustomerId_idx" ON "License"("stripeCustomerId");
CREATE INDEX "LicenseEvent_licenseId_idx" ON "LicenseEvent"("licenseId");
CREATE INDEX "LicenseEvent_createdAt_idx" ON "LicenseEvent"("createdAt");
CREATE INDEX "WebhookEvent_seenAt_idx" ON "WebhookEvent"("seenAt");
CREATE INDEX "UserActivity_userId_idx" ON "UserActivity"("userId");
CREATE INDEX "UserActivity_activityType_idx" ON "UserActivity"("activityType");
CREATE INDEX "UserActivity_createdAt_idx" ON "UserActivity"("createdAt");

-- Verify tables were created
SELECT
  schemaname,
  tablename,
  (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = tablename) as column_count
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('User', 'License', 'WebhookEvent', 'LicenseEvent', 'UserActivity')
ORDER BY tablename;
