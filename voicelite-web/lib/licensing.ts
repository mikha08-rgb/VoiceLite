import { LicenseActivationStatus, LicenseStatus, LicenseType } from '@prisma/client';
import { nanoid } from 'nanoid';
import { prisma } from '@/lib/prisma';

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
      email: normalizedEmail,
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
      email: normalizedEmail,
      status,
      stripeCustomerId,
      stripeSubscriptionId: stripeSubscriptionId ?? undefined,
      stripePaymentIntentId: stripePaymentIntentId ?? undefined,
      expiresAt: periodEndsAt ?? undefined,
      activatedAt: new Date(),
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
    include: { activations: true },
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
  // Check if this is an existing activation (re-validation)
  const existing = await prisma.licenseActivation.findUnique({
    where: {
      licenseId_machineId: {
        licenseId,
        machineId,
      },
    },
  });

  if (existing) {
    // Update existing activation
    return prisma.licenseActivation.update({
      where: {
        licenseId_machineId: {
          licenseId,
          machineId,
        },
      },
      data: {
        machineLabel,
        machineHash,
        lastValidatedAt: new Date(),
        status: LicenseActivationStatus.ACTIVE,
      },
    });
  }

  // New activation - check 3-device limit
  const activeCount = await prisma.licenseActivation.count({
    where: {
      licenseId,
      status: LicenseActivationStatus.ACTIVE,
    },
  });

  if (activeCount >= 3) {
    throw new Error('ACTIVATION_LIMIT_REACHED: Maximum 3 devices allowed per license. Deactivate a device to continue.');
  }

  // Create new activation
  return prisma.licenseActivation.create({
    data: {
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