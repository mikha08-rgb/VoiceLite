-- CreateIndex
-- Composite index for efficient activation count queries
-- Used by: lib/licensing.ts:recordLicenseActivation() to count active activations per license
-- Query pattern: WHERE licenseId = ? AND status = 'ACTIVE'
CREATE INDEX "LicenseActivation_licenseId_status_idx" ON "LicenseActivation"("licenseId", "status");
