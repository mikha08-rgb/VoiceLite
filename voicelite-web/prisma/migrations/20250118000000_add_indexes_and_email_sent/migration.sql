-- Migration: Add indexes and emailSent field for security improvements
-- Date: 2025-01-18
-- Description: Adds performance indexes and email tracking for failed email recovery

-- Add emailSent field to License table
ALTER TABLE "License" ADD COLUMN "emailSent" BOOLEAN NOT NULL DEFAULT false;

-- Add composite index for common query pattern (email + status)
CREATE INDEX "License_email_status_idx" ON "License"("email", "status");

-- Add index for webhook payment intent lookups (performance)
CREATE INDEX "License_stripePaymentIntentId_idx" ON "License"("stripePaymentIntentId");

-- Add index for admin queries to find failed email sends
CREATE INDEX "License_emailSent_idx" ON "License"("emailSent");

-- Add index for machine ID lookups (device management)
CREATE INDEX "LicenseActivation_machineId_idx" ON "LicenseActivation"("machineId");

-- Note: Existing indexes from schema that are already present:
-- - License_email_idx (already exists)
-- - License_stripeCustomerId_idx (already exists)
-- - LicenseActivation_licenseId_idx (already exists)
-- - LicenseActivation_licenseId_machineId_key (unique constraint, already exists)
