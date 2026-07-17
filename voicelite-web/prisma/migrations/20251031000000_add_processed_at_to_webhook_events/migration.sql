-- Add processedAt field to WebhookEvent for atomic race condition prevention
ALTER TABLE "WebhookEvent" ADD COLUMN "processedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP;
