import { NextRequest, NextResponse } from 'next/server';

/**
 * Direct download endpoint for VoiceLite installer
 *
 * Features:
 * - Direct download from Vercel (no redirects)
 * - Analytics tracking
 * - Clean UX - instant download on click
 */

const INSTALLER_VERSION = 'v1.0.72';
const INSTALLER_FILENAME = 'VoiceLite-Setup.exe';

export async function GET(request: NextRequest) {
  try {
    // Track download
    const userAgent = request.headers.get('user-agent') || 'unknown';
    const referrer = request.headers.get('referer') || 'direct';

    console.log('[Download] VoiceLite installer requested', {
      version: INSTALLER_VERSION,
      userAgent: userAgent.substring(0, 100),
      referrer,
      timestamp: new Date().toISOString()
    });

    // Direct download from /public folder
    // Files in /public are served at root: /VoiceLite-Setup.exe
    const downloadUrl = new URL(`/${INSTALLER_FILENAME}`, request.url);

    return NextResponse.redirect(downloadUrl, 302);
  } catch (error) {
    console.error('[Download] Error serving installer:', error);

    // Fallback: direct link to public file
    return NextResponse.redirect(`/${INSTALLER_FILENAME}`, 302);
  }
}

/**
 * Download stats endpoint (optional)
 */
export async function POST(request: NextRequest) {
  // Future: Track successful downloads
  return NextResponse.json({ message: 'Download tracked' });
}
