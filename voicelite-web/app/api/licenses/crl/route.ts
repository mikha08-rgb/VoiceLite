import { NextRequest, NextResponse } from 'next/server';
import { signCRL, CRLPayload } from '@/lib/ed25519';
import { getRevokedLicenseIds } from '@/lib/licensing';
import { getSessionTokenFromRequest, getSessionFromToken } from '@/lib/auth/session';
import { licenseRateLimit, checkRateLimit } from '@/lib/ratelimit';

/**
 * GET /api/licenses/crl
 * Returns a signed Certificate Revocation List (CRL) containing all revoked license IDs.
 * Clients should fetch this periodically and check against it.
 * Requires authentication to prevent enumeration attacks.
 */
export async function GET(request: NextRequest) {
  try {
    // Require authentication
    const sessionToken = getSessionTokenFromRequest(request);
    if (!sessionToken) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    const session = await getSessionFromToken(sessionToken);
    if (!session) {
      return NextResponse.json({ error: 'Authentication required' }, { status: 401 });
    }

    // Rate limiting (30 requests per day per user)
    const rateLimit = await checkRateLimit(session.userId, licenseRateLimit);
    if (!rateLimit.allowed) {
      return NextResponse.json(
        { error: `Rate limit exceeded. Try again after ${rateLimit.reset.toLocaleTimeString()}.` },
        { status: 429, headers: { 'Retry-After': String(Math.ceil((rateLimit.reset.getTime() - Date.now()) / 1000)) } }
      );
    }

    const revokedLicenseIds = await getRevokedLicenseIds();

    const payload: CRLPayload = {
      version: 1,
      updated_at: new Date().toISOString(),
      revoked_license_ids: revokedLicenseIds,
      key_version: 1,
    };

    const signedCRL = await signCRL(payload);

    return NextResponse.json({
      crl: signedCRL,
      count: revokedLicenseIds.length,
    });
  } catch (error) {
    console.error('CRL generation failed:', error);
    return NextResponse.json({ error: 'Unable to generate CRL' }, { status: 500 });
  }
}
