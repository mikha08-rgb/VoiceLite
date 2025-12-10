import { NextRequest, NextResponse } from 'next/server';

// Validate version format: must be X.X.X.X (e.g., 1.2.0.1)
const VERSION_REGEX = /^\d+\.\d+\.\d+\.\d+$/;
const MAX_VERSION_LENGTH = 20;

export async function GET(request: NextRequest) {
  const searchParams = request.nextUrl.searchParams;
  const version = searchParams.get('version') || '1.2.0.4';

  // Validate version format to prevent path traversal
  if (!VERSION_REGEX.test(version)) {
    return NextResponse.json(
      { error: 'Invalid version format. Expected format: X.X.X.X' },
      { status: 400 }
    );
  }

  // Additional length check
  if (version.length > MAX_VERSION_LENGTH) {
    return NextResponse.json(
      { error: 'Version string too long' },
      { status: 400 }
    );
  }

  // GitHub releases direct download URL (triggers immediate download, no GitHub page)
  const downloadUrl = `https://github.com/mikha08-rgb/VoiceLite/releases/download/v${version}/VoiceLite-Setup-${version}.exe`;

  // Fetch the file and stream it to the client with proper download headers
  const response = await fetch(downloadUrl);

  if (!response.ok) {
    return new Response('Download not found', { status: 404 });
  }

  // Size limit check: max 200MB to prevent abuse
  const MAX_SIZE_BYTES = 200 * 1024 * 1024; // 200MB
  const contentLength = response.headers.get('content-length');
  if (contentLength && parseInt(contentLength, 10) > MAX_SIZE_BYTES) {
    return new Response('File too large', { status: 413 });
  }

  // Stream the file with download headers (prevents redirect to GitHub)
  return new Response(response.body, {
    status: 200,
    headers: {
      'Content-Type': 'application/octet-stream',
      'Content-Disposition': `attachment; filename="VoiceLite-Setup-${version}.exe"`,
      'Cache-Control': 'public, max-age=3600',
    },
  });
}
