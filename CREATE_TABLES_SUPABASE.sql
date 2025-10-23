-- VoiceLite Database Schema - Run this in Supabase SQL Editor
-- IMPORTANT: Column names must be quoted because Prisma uses camelCase

-- Create User table
CREATE TABLE IF NOT EXISTS "User" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  email TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  "updatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Create License table
CREATE TABLE IF NOT EXISTS "License" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "userId" TEXT NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
  "licenseKey" TEXT UNIQUE NOT NULL,
  type TEXT NOT NULL DEFAULT 'LIFETIME',
  status TEXT NOT NULL DEFAULT 'ACTIVE',
  "stripeCustomerId" TEXT,
  "stripeSubscriptionId" TEXT UNIQUE,
  "stripePaymentIntentId" TEXT UNIQUE,
  "activatedAt" TIMESTAMP,
  "expiresAt" TIMESTAMP,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  "updatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Create WebhookEvent table (prevents duplicate webhook processing)
CREATE TABLE IF NOT EXISTS "WebhookEvent" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "eventId" TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Create LicenseEvent table (audit log for license changes)
CREATE TABLE IF NOT EXISTS "LicenseEvent" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "licenseId" TEXT NOT NULL REFERENCES "License"(id) ON DELETE CASCADE,
  type TEXT NOT NULL,
  metadata JSONB,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Create UserActivity table (tracks user actions)
CREATE TABLE IF NOT EXISTS "UserActivity" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "userId" TEXT NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
  "activityType" TEXT NOT NULL,
  metadata JSONB,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS "License_userId_idx" ON "License"("userId");
CREATE INDEX IF NOT EXISTS "License_stripeCustomerId_idx" ON "License"("stripeCustomerId");
CREATE INDEX IF NOT EXISTS "LicenseEvent_licenseId_idx" ON "LicenseEvent"("licenseId");
CREATE INDEX IF NOT EXISTS "UserActivity_userId_idx" ON "UserActivity"("userId");

-- Verify tables were created
SELECT
  table_name,
  (SELECT COUNT(*) FROM information_schema.columns WHERE table_name = t.table_name) as column_count
FROM information_schema.tables t
WHERE table_schema = 'public'
  AND table_name IN ('User', 'License', 'WebhookEvent', 'LicenseEvent', 'UserActivity')
ORDER BY table_name;
