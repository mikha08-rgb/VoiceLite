import fs from 'fs/promises';
import path from 'path';

// Simple file-based storage for licenses (upgrade to database later)
const LICENSES_FILE = path.join(process.cwd(), 'data', 'licenses.json');

export interface License {
  email: string;
  licenseKey: string;
  customerId: string;
  subscriptionId: string;
  createdAt: string;
  status: 'active' | 'canceled' | 'expired';
}

// Ensure data directory exists
async function ensureDataDir() {
  const dataDir = path.join(process.cwd(), 'data');
  try {
    await fs.access(dataDir);
  } catch {
    await fs.mkdir(dataDir, { recursive: true });
  }
}

// Load all licenses from file
async function loadLicenses(): Promise<License[]> {
  await ensureDataDir();
  try {
    const data = await fs.readFile(LICENSES_FILE, 'utf-8');
    return JSON.parse(data);
  } catch (error) {
    // File doesn't exist yet, return empty array
    return [];
  }
}

// Save all licenses to file
async function saveLicenses(licenses: License[]): Promise<void> {
  await ensureDataDir();
  await fs.writeFile(LICENSES_FILE, JSON.stringify(licenses, null, 2));
}

// Save a new license
export async function saveLicense(license: License): Promise<void> {
  const licenses = await loadLicenses();

  // Check if license already exists for this email
  const existingIndex = licenses.findIndex(l => l.email === license.email);

  if (existingIndex >= 0) {
    // Update existing license
    licenses[existingIndex] = license;
  } else {
    // Add new license
    licenses.push(license);
  }

  await saveLicenses(licenses);
}

// Get license by email
export async function getLicenseByEmail(email: string): Promise<License | null> {
  const licenses = await loadLicenses();
  return licenses.find(l => l.email === email) || null;
}

// Get license by key
export async function getLicenseByKey(licenseKey: string): Promise<License | null> {
  const licenses = await loadLicenses();
  return licenses.find(l => l.licenseKey === licenseKey) || null;
}

// Update license status
export async function updateLicenseStatus(
  subscriptionId: string,
  status: License['status']
): Promise<void> {
  const licenses = await loadLicenses();
  const license = licenses.find(l => l.subscriptionId === subscriptionId);

  if (license) {
    license.status = status;
    await saveLicenses(licenses);
  }
}

// Validate license key format
export function isValidLicenseKey(key: string): boolean {
  // VL-TIMESTAMP-RANDOM format
  const pattern = /^VL-[A-Z0-9]+-[A-Z0-9]{9}$/;
  return pattern.test(key);
}