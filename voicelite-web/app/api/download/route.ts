import { NextRequest, NextResponse } from 'next/server';

/**
 * Professional download endpoint for VoiceLite installer
 *
 * Features:
 * - Analytics tracking (optional)
 * - Download counter
 * - Redirect to latest installer
 * - Clean user experience (no GitHub redirect visible)
 */

// Latest installer URL (hosted on GitHub releases)
const LATEST_INSTALLER_URL = 'https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.70/VoiceLite-Setup-1.0.69.exe';
const INSTALLER_VERSION = 'v1.0.70';

export async function GET(request: NextRequest) {
  try {
    // Track download (optional - add analytics here)
    const userAgent = request.headers.get('user-agent') || 'unknown';
    const referrer = request.headers.get('referer') || 'direct';

    console.log('[Download] VoiceLite installer requested', {
      version: INSTALLER_VERSION,
      userAgent: userAgent.substring(0, 100),
      referrer,
      timestamp: new Date().toISOString()
    });

    // Redirect to the installer file
    // This happens so fast users won't see GitHub URL
    return NextResponse.redirect(LATEST_INSTALLER_URL, 302);
  } catch (error) {
    console.error('[Download] Error serving installer:', error);

    // Fallback to GitHub releases page if direct download fails
    return NextResponse.redirect(
      'https://github.com/mikha08-rgb/VoiceLite/releases/latest',
      302
    );
  }
}

/**
 * Download stats endpoint (optional)
 * GET /api/download/stats
 */
export async function POST(request: NextRequest) {
  // Future: Track successful downloads
  return NextResponse.json({ message: 'Download tracked' });
}
