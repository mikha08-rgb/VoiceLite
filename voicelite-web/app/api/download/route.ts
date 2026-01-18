import { NextRequest, NextResponse } from 'next/server';

// Validate version format: must be X.X.X.X (e.g., 1.2.0.1)
const VERSION_REGEX = /^\d+\.\d+\.\d+\.\d+$/;
const MAX_VERSION_LENGTH = 20;
const FETCH_TIMEOUT_MS = 30000; // 30 seconds
const DEFAULT_VERSION = process.env.NEXT_PUBLIC_CURRENT_VERSION || '1.2.0.8';

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

  try {
    // Fetch with timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    const response = await fetch(downloadUrl, { signal: controller.signal });
    clearTimeout(timeoutId);

    if (!response.ok) {
      console.log(`[download] FAIL not_found version=${version} status=${response.status} ip=${maskedIp}`);
      return new Response('Download not found', { status: 404 });
    }

    // Size limit check: max 200MB to prevent abuse
    const MAX_SIZE_BYTES = 200 * 1024 * 1024; // 200MB
    const contentLength = response.headers.get('content-length');
    if (contentLength && parseInt(contentLength, 10) > MAX_SIZE_BYTES) {
      console.log(`[download] FAIL file_too_large version=${version} size=${contentLength} ip=${maskedIp}`);
      return new Response('File too large', { status: 413 });
    }

    console.log(`[download] OK version=${version} ip=${maskedIp}`);

    // Stream the file with download headers (prevents redirect to GitHub)
    return new Response(response.body, {
      status: 200,
      headers: {
        'Content-Type': 'application/octet-stream',
        'Content-Disposition': `attachment; filename="VoiceLite-Setup-${version}.exe"`,
        'Cache-Control': 'public, max-age=3600',
      },
    });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      console.log(`[download] FAIL timeout version=${version} ip=${maskedIp}`);
      return new Response('Download timed out. Please try again.', { status: 504 });
    }
    console.log(`[download] FAIL fetch_error version=${version} error=${error} ip=${maskedIp}`);
    return new Response('Download failed. Please try again.', { status: 502 });
  }
}
