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
      // Fail-closed: unknown statuses default to EXPIRED for security
      // If Stripe adds new status types, licenses won't be granted by default
      return LicenseStatus.EXPIRED;
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

  // Use transaction to ensure user and license are created atomically
  // Prevents orphaned user records if license creation fails
  return prisma.$transaction(async (tx) => {
    // Create or find user for this email (required by schema)
    const user = await tx.user.upsert({
      where: { email: normalizedEmail },
      create: {
        id: `user_${Date.now()}_${Math.random().toString(36).substring(7)}`,
        email: normalizedEmail,
      },
      update: {},
    });

    const where = stripeSubscriptionId
      ? { stripeSubscriptionId }
      : { stripePaymentIntentId: stripePaymentIntentId! };

    const license = await tx.license.upsert({
      where,
      create: {
        email: normalizedEmail,
        licenseKey,
        type,
        status,
        userId: user.id,
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
  });
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
  // CRITICAL-4 FIX (v2): count-then-create must run under SERIALIZABLE isolation.
  // Merely putting both operations in a transaction was NOT enough: at Postgres's
  // default READ COMMITTED level, two concurrent NEW machineIds could both count 2,
  // both create, and end up at 4 activations. Serializable makes one of the two
  // conflicting transactions fail with P2034 (serialization failure), which we
  // retry below; the retry then sees the committed count and correctly hits the cap.
  //
  // P2002 handling: two concurrent FIRST-EVER validations of the SAME machineId
  // (including the shared 'legacy-no-machine-id' slot) can both pass findUnique
  // and race the create; the loser gets a unique-constraint violation. We retry
  // the whole transaction, which then takes the existing-activation update path
  // instead of bubbling a 500. (The update can't be issued inside the same
  // transaction after the violation - Postgres aborts the tx - so retrying is
  // the correct conversion to the update path.)
  const MAX_ATTEMPTS = 3;
  let lastError: unknown;

  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    try {
      return await prisma.$transaction(async (tx) => {
        // Check if this is an existing activation (re-validation) - inside transaction
        const existing = await tx.licenseActivation.findUnique({
          where: {
            licenseId_machineId: {
              licenseId,
              machineId,
            },
          },
        });

        if (existing) {
          // Re-validation path. If the row is still ACTIVE this is a normal
          // heartbeat - refresh it unconditionally (current behavior unchanged).
          //
          // DEACTIVATION-BYPASS FIX: if the row is NOT active (the deactivate
          // endpoint sets BLOCKED), flipping it back to ACTIVE must pass the same
          // 3-device cap as a brand-new activation. Previously this path set
          // ACTIVE unconditionally, so a deactivated machine re-activated itself
          // on its next revalidation, and deactivate -> activate-new ->
          // revalidate-old yielded 4+ ACTIVE devices. Explicit reactivation is
          // still legitimate when a slot is free.
          if (existing.status !== LicenseActivationStatus.ACTIVE) {
            const reactivationActiveCount = await tx.licenseActivation.count({
              where: {
                licenseId,
                status: LicenseActivationStatus.ACTIVE,
              },
            });

            if (reactivationActiveCount >= 3) {
              throw new Error('ACTIVATION_LIMIT_REACHED: Maximum 3 devices allowed per license. Deactivate a device to continue.');
            }
          }

          // Update existing activation - inside transaction
          return tx.licenseActivation.update({
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

        // New activation - check 3-device limit (inside transaction)
        const activeCount = await tx.licenseActivation.count({
          where: {
            licenseId,
            status: LicenseActivationStatus.ACTIVE,
          },
        });

        if (activeCount >= 3) {
          throw new Error('ACTIVATION_LIMIT_REACHED: Maximum 3 devices allowed per license. Deactivate a device to continue.');
        }

        // Create new activation (inside same transaction)
        return tx.licenseActivation.create({
          data: {
            licenseId,
            machineId,
            machineLabel,
            machineHash,
          },
        });
      }, { isolationLevel: 'Serializable' });
    } catch (error: any) {
      // P2034 = serialization/deadlock failure, P2002 = unique constraint race on
      // the create - both are transient concurrency losses; retry the transaction.
      if ((error?.code === 'P2034' || error?.code === 'P2002') && attempt < MAX_ATTEMPTS) {
        lastError = error;
        continue;
      }
      throw error;
    }
  }

  // Unreachable (loop either returns or throws), but keeps TypeScript satisfied.
  throw lastError;
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