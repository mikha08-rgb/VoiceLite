/**
 * OpenAPI JSON Endpoint
 *
 * Serves the OpenAPI 3.0 JSON specification at /api/docs
 * Used by Swagger UI and other API clients
 */

import { NextResponse } from 'next/server';
import { generateOpenAPIDocument } from '@/lib/openapi';

export async function GET() {
  const openApiDocument = generateOpenAPIDocument();

  return NextResponse.json(openApiDocument, {
    headers: {
      'Content-Type': 'application/json',
      'Cache-Control': 'public, max-age=3600', // Cache for 1 hour
    },
  });
}
