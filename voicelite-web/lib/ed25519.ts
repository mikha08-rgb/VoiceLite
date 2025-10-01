import * as ed from '@noble/ed25519';

/**
 * Ed25519 signing and verification for VoiceLite licenses and CRL.
 * Keys are stored as base64url-encoded strings in environment variables.
 */

export interface LicensePayload {
  license_id: string;
  user_id: string;
  product_id: string;
  plan: 'pro' | 'lifetime';
  device_fingerprint: string;
  seat_limit: number;
  issued_at: string; // ISO8601
  expires_at: string; // ISO8601
  grace_days: number;
  key_version: number;
  version: number;
}

export interface CRLPayload {
  version: number;
  updated_at: string; // ISO8601
  revoked_license_ids: string[];
  key_version: number;
}

/**
 * Canonical JSON serialization (deterministic key ordering)
 */
function canonicalJSON(obj: Record<string, any>): string {
  const sortedKeys = Object.keys(obj).sort();
  const sorted: Record<string, any> = {};
  for (const key of sortedKeys) {
    sorted[key] = obj[key];
  }
  return JSON.stringify(sorted);
}

/**
 * Convert base64url string to Uint8Array
 */
function base64urlToBytes(base64url: string): Uint8Array {
  const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const binary = Buffer.from(base64 + padding, 'base64');
  return new Uint8Array(binary);
}

/**
 * Convert Uint8Array to base64url string
 */
function bytesToBase64url(bytes: Uint8Array): string {
  const base64 = Buffer.from(bytes).toString('base64');
  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

/**
 * Sign a license payload with Ed25519
 * @returns Signed license string: base64url(payload).base64url(signature)
 */
export async function signLicense(payload: LicensePayload): Promise<string> {
  const privateKeyB64 = process.env.LICENSE_SIGNING_PRIVATE_B64;
  if (!privateKeyB64) {
    throw new Error('LICENSE_SIGNING_PRIVATE_B64 not configured');
  }

  const privateKey = base64urlToBytes(privateKeyB64);
  const message = new TextEncoder().encode(canonicalJSON(payload));
  const signature = await ed.signAsync(message, privateKey);

  const payloadB64 = bytesToBase64url(new TextEncoder().encode(canonicalJSON(payload)));
  const signatureB64 = bytesToBase64url(signature);

  return `${payloadB64}.${signatureB64}`;
}

/**
 * Verify a signed license string
 * @returns Parsed license payload if valid, null if invalid
 */
export async function verifyLicense(
  signedLicense: string,
  publicKeyB64?: string
): Promise<LicensePayload | null> {
  const parts = signedLicense.split('.');
  if (parts.length !== 2) {
    return null;
  }

  const [payloadB64, signatureB64] = parts;
  const publicKey = base64urlToBytes(
    publicKeyB64 ?? process.env.LICENSE_SIGNING_PUBLIC_B64 ?? ''
  );

  if (publicKey.length === 0) {
    throw new Error('LICENSE_SIGNING_PUBLIC_B64 not configured');
  }

  try {
    const message = base64urlToBytes(payloadB64);
    const signature = base64urlToBytes(signatureB64);

    const isValid = await ed.verifyAsync(signature, message, publicKey);
    if (!isValid) {
      return null;
    }

    const payload = JSON.parse(new TextDecoder().decode(message)) as LicensePayload;
    return payload;
  } catch {
    return null;
  }
}

/**
 * Sign a CRL (Certificate Revocation List)
 * @returns Signed CRL string: base64url(payload).base64url(signature)
 */
export async function signCRL(payload: CRLPayload): Promise<string> {
  const privateKeyB64 = process.env.CRL_SIGNING_PRIVATE_B64 ?? process.env.LICENSE_SIGNING_PRIVATE_B64;
  if (!privateKeyB64) {
    throw new Error('CRL_SIGNING_PRIVATE_B64 or LICENSE_SIGNING_PRIVATE_B64 not configured');
  }

  const privateKey = base64urlToBytes(privateKeyB64);
  const message = new TextEncoder().encode(canonicalJSON(payload));
  const signature = await ed.signAsync(message, privateKey);

  const payloadB64 = bytesToBase64url(new TextEncoder().encode(canonicalJSON(payload)));
  const signatureB64 = bytesToBase64url(signature);

  return `${payloadB64}.${signatureB64}`;
}

/**
 * Verify a signed CRL
 * @returns Parsed CRL payload if valid, null if invalid
 */
export async function verifyCRL(
  signedCRL: string,
  publicKeyB64?: string
): Promise<CRLPayload | null> {
  const parts = signedCRL.split('.');
  if (parts.length !== 2) {
    return null;
  }

  const [payloadB64, signatureB64] = parts;
  const publicKey = base64urlToBytes(
    publicKeyB64 ?? process.env.CRL_SIGNING_PUBLIC_B64 ?? process.env.LICENSE_SIGNING_PUBLIC_B64 ?? ''
  );

  if (publicKey.length === 0) {
    throw new Error('CRL_SIGNING_PUBLIC_B64 or LICENSE_SIGNING_PUBLIC_B64 not configured');
  }

  try {
    const message = base64urlToBytes(payloadB64);
    const signature = base64urlToBytes(signatureB64);

    const isValid = await ed.verifyAsync(signature, message, publicKey);
    if (!isValid) {
      return null;
    }

    const payload = JSON.parse(new TextDecoder().decode(message)) as CRLPayload;
    return payload;
  } catch {
    return null;
  }
}

/**
 * Generate a new Ed25519 keypair for testing/setup
 * @returns {privateKey, publicKey} as base64url strings
 */
export async function generateKeypair(): Promise<{ privateKey: string; publicKey: string }> {
  // Generate 32 random bytes for private key
  const privateKey = crypto.getRandomValues(new Uint8Array(32));
  const publicKey = await ed.getPublicKeyAsync(privateKey);

  return {
    privateKey: bytesToBase64url(privateKey),
    publicKey: bytesToBase64url(publicKey),
  };
}
