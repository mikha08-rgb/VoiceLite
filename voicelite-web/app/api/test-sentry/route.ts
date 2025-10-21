import * as Sentry from '@sentry/nextjs';
import { NextResponse } from 'next/server';

export async function GET() {
  // SECURITY: Disable test endpoint in production
  if (process.env.NODE_ENV === 'production') {
    return NextResponse.json(
      { error: 'Not found' },
      { status: 404 }
    );
  }

  // This will send a test error to Sentry (development only)
  Sentry.captureException(new Error('Test error from VoiceLite API - Sentry is working!'));
  return NextResponse.json({
    message: 'Test error sent to Sentry',
    note: 'Check your Sentry dashboard - you should see this error within 30 seconds!'
  });
}
