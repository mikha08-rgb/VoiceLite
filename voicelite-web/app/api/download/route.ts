import { NextRequest, NextResponse } from 'next/server';

export async function GET(request: NextRequest) {
  const searchParams = request.nextUrl.searchParams;
  const version = searchParams.get('version') || '1.0.76';

  // Redirect to GitHub release
  const downloadUrl = `https://github.com/mikha08-rgb/VoiceLite/releases/download/v${version}/VoiceLite-Setup-${version}.exe`;

  return NextResponse.redirect(downloadUrl, 302);
}
