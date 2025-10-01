import { NextRequest, NextResponse } from 'next/server';
import { clearSessionCookie } from '@/lib/auth/session';

export async function POST(request: NextRequest) {
  const response = NextResponse.json({ ok: true });
  await clearSessionCookie(response, request);
  return response;
}