-- TelemetryMetric table creation for VoiceLite production monitoring
-- Run this in Supabase SQL Editor

CREATE TABLE IF NOT EXISTS "TelemetryMetric" (
    "id" TEXT NOT NULL,
    "anonymousUserId" TEXT NOT NULL,
    "metricType" TEXT NOT NULL,
    "value" DOUBLE PRECISION NOT NULL,
    "metadata" TEXT,
    "timestamp" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "TelemetryMetric_pkey" PRIMARY KEY ("id")
);

CREATE INDEX IF NOT EXISTS "TelemetryMetric_anonymousUserId_idx" ON "TelemetryMetric"("anonymousUserId");
CREATE INDEX IF NOT EXISTS "TelemetryMetric_metricType_idx" ON "TelemetryMetric"("metricType");
CREATE INDEX IF NOT EXISTS "TelemetryMetric_timestamp_idx" ON "TelemetryMetric"("timestamp");
