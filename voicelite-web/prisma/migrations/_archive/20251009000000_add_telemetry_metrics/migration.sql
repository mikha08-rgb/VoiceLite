-- CreateTable
CREATE TABLE "TelemetryMetric" (
    "id" TEXT NOT NULL,
    "anonymousUserId" TEXT NOT NULL,
    "metricType" TEXT NOT NULL,
    "value" DOUBLE PRECISION NOT NULL,
    "metadata" TEXT,
    "timestamp" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "TelemetryMetric_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE INDEX "TelemetryMetric_anonymousUserId_idx" ON "TelemetryMetric"("anonymousUserId");

-- CreateIndex
CREATE INDEX "TelemetryMetric_metricType_idx" ON "TelemetryMetric"("metricType");

-- CreateIndex
CREATE INDEX "TelemetryMetric_timestamp_idx" ON "TelemetryMetric"("timestamp");
