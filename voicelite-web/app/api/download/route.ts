import { NextRequest, NextResponse } from 'next/server';

// Validate version format: must be X.X.X or X.X.X.X (e.g., 2.0.1 or 1.2.0.1)
// v2.0+ uses 3-part versions for the user-facing tag; pre-v2 used 4-part.
const VERSION_REGEX = /^\d+\.\d+\.\d+(\.\d+)?$/;
const MAX_VERSION_LENGTH = 20;
// Hardcoded — bump alongside csproj <Version> and iss MyAppVersion on each release.
// Previously read from NEXT_PUBLIC_CURRENT_VERSION env var, which silently lagged
// behind code on every ship (the env var lived in Vercel UI and got forgotten).
const DEFAULT_VERSION = '2.1.2';

// Mask IP for privacy (show first octet only)
function maskIp(ip: string): string {
  const parts = ip.split('.');
  if (parts.length === 4) {
    return `${parts[0]}.*.*.*`;
  }
  // IPv6 or other format - just show first segment
  return ip.split(':')[0] + ':***';
}

export async function GET(request: NextRequest) {
  const searchParams = request.nextUrl.searchParams;
  const version = searchParams.get('version') || DEFAULT_VERSION;
  const ip = request.headers.get('x-forwarded-for')?.split(',')[0] || 'unknown';
  const maskedIp = maskIp(ip);

  // Validate version format to prevent path traversal
  if (!VERSION_REGEX.test(version)) {
    console.log(`[download] FAIL invalid_format version=${version} ip=${maskedIp}`);
    return NextResponse.json(
      { error: 'Invalid version format. Expected format: X.X.X.X' },
      { status: 400 }
    );
  }

  // Additional length check
  if (version.length > MAX_VERSION_LENGTH) {
    console.log(`[download] FAIL version_too_long version=${version} ip=${maskedIp}`);
    return NextResponse.json(
      { error: 'Version string too long' },
      { status: 400 }
    );
  }

  // GitHub releases direct download URL (triggers immediate download, no GitHub page)
  const downloadUrl = `https://github.com/mikha08-rgb/VoiceLite/releases/download/v${version}/VoiceLite-Setup-${version}.exe`;

  console.log(`[download] OK version=${version} ip=${maskedIp}`);

  // Redirect to GitHub release asset — file is too large to proxy through serverless.
  // GitHub will return 404 itself if the release doesn't exist.
  return NextResponse.redirect(downloadUrl, { status: 302 });
}
