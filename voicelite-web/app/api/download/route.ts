import { NextRequest } from 'next/server';

export async function GET(request: NextRequest) {
  const searchParams = request.nextUrl.searchParams;
  const version = searchParams.get('version') || '1.0.91';

  // GitHub releases direct download URL (triggers immediate download, no GitHub page)
  const downloadUrl = `https://github.com/mikha08-rgb/VoiceLite/releases/download/v${version}/VoiceLite-Setup-${version}.exe`;

  // Fetch the file and stream it to the client with proper download headers
  const response = await fetch(downloadUrl);

  if (!response.ok) {
    return new Response('Download not found', { status: 404 });
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
