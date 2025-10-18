import { LicenseStatus, LicenseType } from '@prisma/client';
import { nanoid } from 'nanoid';
import { prisma } from '@/lib/prisma';

export function generateLicenseKey() {
  const segment = () => nanoid(6).toUpperCase();
  return `VL-${segment()}-${segment()}-${segment()}`;
}

interface CreateLicenseArgs {
  email: string;
  stripeCustomerId: string;
  stripePaymentIntentId: string;
}

/**
 * Create a new license from a Stripe payment (simplified - no User table)
 */
export async function createLicenseFromStripe({
  email,
  stripeCustomerId,
  stripePaymentIntentId,
}: CreateLicenseArgs) {
  const normalizedEmail = email.toLowerCase();

  // Check for existing active license for this email
  const existingLicense = await prisma.license.findFirst({
    where: {
      email: normalizedEmail,
      status: LicenseStatus.ACTIVE
    }
  });

  if (existingLicense) {
    // Return existing license instead of creating duplicate
    return existingLicense;
  }

  // Create new license (no User table needed!)
  const licenseKey = generateLicenseKey();

  const license = await prisma.license.create({
    data: {
      email: normalizedEmail,
      licenseKey,
      type: LicenseType.LIFETIME,
      status: LicenseStatus.ACTIVE,
      stripeCustomerId,
      stripePaymentIntentId,
      maxDevices: 3,
    },
  });

  return license;
}

/**
 * Get license by key (simplified - no User relation)
 */
export async function getLicenseByKey(licenseKey: string) {
  return prisma.license.findUnique({
    where: { licenseKey },
    include: { activations: true },
  });
}

/**
 * Record license activation (simplified - no status field)
 */
export async function recordLicenseActivation({
  licenseId,
  machineId,
  machineLabel,
}: {
  licenseId: string;
  machineId: string;
  machineLabel?: string;
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
      lastValidatedAt: new Date(),
    },
    create: {
      licenseId,
      machineId,
      machineLabel,
      lastValidatedAt: new Date(),
    },
  });
}

/**
 * Revoke a license (for refunds)
 */
export async function revokeLicense(licenseId: string) {
  return prisma.license.update({
    where: { id: licenseId },
    data: {
      status: LicenseStatus.CANCELED,
    },
  });
}