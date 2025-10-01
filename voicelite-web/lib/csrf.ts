import { NextRequest } from 'next/server';

/**
 * Validate that the request originates from an allowed origin.
 * Checks Origin and Referer headers to prevent CSRF attacks.
 *
 * @param request - The Next.js request object
 * @returns true if the origin is allowed, false otherwise
 */
export function validateOrigin(request: NextRequest): boolean {
  const origin = request.headers.get('origin');
  const referer = request.headers.get('referer');

  // Get allowed origins from environment
  const appUrl = process.env.NEXT_PUBLIC_APP_URL;
  const allowedOrigins = [
    'https://app.voicelite.com', // Production URL
    appUrl, // Environment-specific URL
    ...(process.env.NODE_ENV === 'development' ? ['http://localhost:3000'] : []),
  ].filter((url): url is string => Boolean(url));

  // Check Origin header (most reliable for CORS requests)
  if (origin) {
    return allowedOrigins.includes(origin);
  }

  // Fallback to Referer header if Origin is not present
  if (referer) {
    try {
      const refererUrl = new URL(referer);
      const refererOrigin = `${refererUrl.protocol}//${refererUrl.host}`;
      return allowedOrigins.includes(refererOrigin);
    } catch {
      return false; // Invalid referer URL
    }
  }

  // If neither header is present, reject the request
  // Modern browsers always send at least one of these headers
  return false;
}

/**
 * Get a standardized CSRF error response
 */
export function getCsrfErrorResponse() {
  return {
    error: 'Invalid request origin',
    message: 'This request appears to come from an unauthorized source. Please ensure you are accessing this service from the official VoiceLite application.',
  };
}
