import { NextRequest, NextResponse } from 'next/server';

/**
 * Download endpoint for VoiceLite installer
 *
 * Features:
 * - Download from GitHub Releases (no file size limits)
 * - Analytics tracking
 * - Clean UX - instant download redirect
 */

const INSTALLER_VERSION = 'v1.0.72';
const GITHUB_REPO = 'mikha08-rgb/VoiceLite';
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

    // Redirect to GitHub Release
    const downloadUrl = `https://github.com/${GITHUB_REPO}/releases/download/${INSTALLER_VERSION}/${INSTALLER_FILENAME}`;

    return NextResponse.redirect(downloadUrl, 302);
  } catch (error) {
    console.error('[Download] Error redirecting to installer:', error);

    // Fallback: direct link to latest release
    const fallbackUrl = `https://github.com/${GITHUB_REPO}/releases/latest/download/${INSTALLER_FILENAME}`;
    return NextResponse.redirect(fallbackUrl, 302);
  }
}

/**
 * Download stats endpoint (optional)
 */
export async function POST(request: NextRequest) {
  // Future: Track successful downloads
  return NextResponse.json({ message: 'Download tracked' });
}
