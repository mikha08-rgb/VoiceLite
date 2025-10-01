import { LicenseActivationStatus, LicenseStatus, LicenseType } from '@prisma/client';
import { nanoid } from 'nanoid';
import { prisma } from '@/lib/prisma';
import { signLicense, LicensePayload } from '@/lib/ed25519';

export function generateLicenseKey() {
  const segment = () => nanoid(6).toUpperCase();
  return `VL-${segment()}-${segment()}-${segment()}`;
}

export function mapStripeSubscriptionStatus(status: string): LicenseStatus {
  switch (status) {
    case 'active':
    case 'trialing':
      return LicenseStatus.ACTIVE;
    case 'canceled':
      return LicenseStatus.CANCELED;
    case 'incomplete_expired':
    case 'unpaid':
    case 'past_due':
      return LicenseStatus.EXPIRED;
    default:
      return LicenseStatus.ACTIVE;
  }
}

interface UpsertLicenseArgs {
  email: string;
  type: LicenseType;
  stripeCustomerId: string;
  stripeSubscriptionId?: string | null;
  stripePaymentIntentId?: string | null;
  subscriptionStatus?: string;
  periodEndsAt?: Date | null;
}

export async function upsertLicenseFromStripe({
  email,
  type,
  stripeCustomerId,
  stripeSubscriptionId,
  stripePaymentIntentId,
  subscriptionStatus,
  periodEndsAt,
}: UpsertLicenseArgs) {
  if (!stripeSubscriptionId && !stripePaymentIntentId) {
    throw new Error('Missing Stripe identifiers for license issuance');
  }

  const normalizedEmail = email.toLowerCase();
  const user = await prisma.user.upsert({
    where: { email: normalizedEmail },
    create: { email: normalizedEmail },
    update: {},
  });

  const licenseKey = generateLicenseKey();
  const status = stripeSubscriptionId
    ? mapStripeSubscriptionStatus(subscriptionStatus ?? 'active')
    : LicenseStatus.ACTIVE;

  const where = stripeSubscriptionId
    ? { stripeSubscriptionId }
    : { stripePaymentIntentId: stripePaymentIntentId! };

  const license = await prisma.license.upsert({
    where,
    create: {
      userId: user.id,
      licenseKey,
      type,
      status,
      stripeCustomerId,
      stripeSubscriptionId: stripeSubscriptionId ?? undefined,
      stripePaymentIntentId: stripePaymentIntentId ?? undefined,
      activatedAt: new Date(),
      expiresAt: periodEndsAt ?? undefined,
    },
    update: {
      userId: user.id,
      status,
      stripeCustomerId,
      stripeSubscriptionId: stripeSubscriptionId ?? undefined,
      stripePaymentIntentId: stripePaymentIntentId ?? undefined,
      expiresAt: periodEndsAt ?? undefined,
      activatedAt: new Date(),
    },
    include: {
      user: true,
    },
  });

  return license;
}

export async function updateLicenseStatusBySubscriptionId(
  stripeSubscriptionId: string,
  subscriptionStatus: string,
  periodEndsAt?: Date | null,
) {
  return prisma.license.updateMany({
    where: { stripeSubscriptionId },
    data: {
      status: mapStripeSubscriptionStatus(subscriptionStatus),
      expiresAt: periodEndsAt ?? undefined,
      updatedAt: new Date(),
    },
  });
}

export async function getLicenseByKey(licenseKey: string) {
  return prisma.license.findUnique({
    where: { licenseKey },
    include: { user: true, activations: true },
  });
}

export async function recordLicenseActivation({
  licenseId,
  machineId,
  machineLabel,
  machineHash,
}: {
  licenseId: string;
  machineId: string;
  machineLabel?: string;
  machineHash?: string;
}) {
  return prisma.licenseActivation.upsert({
    where: {
      licenseId_machineId: {
        licenseId,
        machineId,
      },
    },
    update: {
      machineLabel,
      machineHash,
      lastValidatedAt: new Date(),
      status: LicenseActivationStatus.ACTIVE,
    },
    create: {
      licenseId,
      machineId,
      machineLabel,
      machineHash,
    },
  });
}

export async function deactivateLicenseActivation({
  licenseId,
  machineId,
}: {
  licenseId: string;
  machineId: string;
}) {
  return prisma.licenseActivation.update({
    where: {
      licenseId_machineId: {
        licenseId,
        machineId,
      },
    },
    data: {
      status: LicenseActivationStatus.BLOCKED,
    },
  });
}

/**
 * Record a license event (issued, renewed, revoked, etc.)
 */
export async function recordLicenseEvent(
  licenseId: string,
  type: string,
  metadata?: Record<string, any>
) {
  return prisma.licenseEvent.create({
    data: {
      licenseId,
      type,
      metadata: metadata ? JSON.stringify(metadata) : null,
    },
  });
}

/**
 * Generate a signed license payload for a given license and device
 */
export async function generateSignedLicense(licenseId: string, deviceFingerprint: string) {
  const license = await prisma.license.findUnique({
    where: { id: licenseId },
    include: { user: true },
  });

  if (!license) {
    throw new Error('License not found');
  }

  if (license.status !== LicenseStatus.ACTIVE) {
    throw new Error('License is not active');
  }

  const now = new Date();
  const graceDays = 14;

  // Calculate expiry based on license type
  let expiresAt: Date;
  if (license.type === LicenseType.LIFETIME) {
    // Lifetime: 10 years from now
    expiresAt = new Date(now.getTime() + 10 * 365 * 24 * 60 * 60 * 1000);
  } else {
    // Subscription: use existing expiresAt or 90 days
    expiresAt = license.expiresAt ?? new Date(now.getTime() + 90 * 24 * 60 * 60 * 1000);
  }

  const payload: LicensePayload = {
    license_id: license.id,
    user_id: license.userId,
    product_id: license.type === LicenseType.LIFETIME ? 'voicelite-lifetime' : 'voicelite-pro',
    plan: license.type === LicenseType.LIFETIME ? 'lifetime' : 'pro',
    device_fingerprint: deviceFingerprint,
    seat_limit: 1,
    issued_at: now.toISOString(),
    expires_at: expiresAt.toISOString(),
    grace_days: graceDays,
    key_version: 1,
    version: 1,
  };

  const signedLicense = await signLicense(payload);
  return signedLicense;
}

/**
 * Revoke a license and record the event
 */
export async function revokeLicense(licenseId: string, reason?: string) {
  const license = await prisma.license.update({
    where: { id: licenseId },
    data: {
      status: LicenseStatus.CANCELED,
    },
  });

  await recordLicenseEvent(licenseId, 'revoked', { reason });
  return license;
}

/**
 * Get all revoked license IDs for CRL
 */
export async function getRevokedLicenseIds(): Promise<string[]> {
  const revokedLicenses = await prisma.license.findMany({
    where: { status: LicenseStatus.CANCELED },
    select: { id: true },
  });

  return revokedLicenses.map((l) => l.id);
}