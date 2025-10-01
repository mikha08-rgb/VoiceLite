import { NextRequest } from 'next/server';
import { prisma } from '@/lib/prisma';
import { getSessionTokenFromRequest, getSessionFromToken, rotateSession } from '@/lib/auth/session';

export async function getAuthenticatedUser(request: NextRequest) {
  const sessionToken = getSessionTokenFromRequest(request);
  if (!sessionToken) {
    return { user: null, session: null, rotation: null } as const;
  }

  const session = await getSessionFromToken(sessionToken);
  if (!session) {
    return { user: null, session: null, rotation: null } as const;
  }

  const { session: updated, token: newToken, expiresAt } = await rotateSession(session.id);
  const user = await prisma.user.findUnique({ where: { id: updated.userId } });

  return {
    user,
    session: updated,
    rotation: { token: newToken, expiresAt },
  } as const;
}