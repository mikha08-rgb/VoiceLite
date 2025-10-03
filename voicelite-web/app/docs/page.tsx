'use client';

/**
 * API Documentation Page (Swagger UI)
 *
 * Interactive API documentation at /docs
 * Consumes OpenAPI spec from /api/docs
 */

import { useEffect, useRef } from 'react';
import SwaggerUI from 'swagger-ui-react';
import 'swagger-ui-react/swagger-ui.css';

export default function APIDocsPage() {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Apply dark mode styles if needed
    if (containerRef.current) {
      const isDark = document.documentElement.classList.contains('dark');
      if (isDark) {
        containerRef.current.classList.add('swagger-dark');
      }
    }
  }, []);

  return (
    <div className="min-h-screen bg-white dark:bg-[#0f0f12]">
      {/* Header */}
      <div className="border-b border-stone-200 bg-white px-6 py-4 dark:border-stone-800 dark:bg-stone-900">
        <div className="mx-auto max-w-7xl">
          <h1 className="text-2xl font-bold text-stone-900 dark:text-stone-50">
            VoiceLite API Documentation
          </h1>
          <p className="mt-1 text-sm text-stone-600 dark:text-stone-400">
            Interactive API documentation powered by OpenAPI 3.0
          </p>
        </div>
      </div>

      {/* Swagger UI */}
      <div ref={containerRef} className="swagger-container">
        <SwaggerUI
          url="/api/docs"
          deepLinking={true}
          displayRequestDuration={true}
          filter={true}
          tryItOutEnabled={true}
          persistAuthorization={true}
          docExpansion="list"
          defaultModelsExpandDepth={1}
          defaultModelExpandDepth={1}
        />
      </div>

      {/* Custom styles for Swagger UI */}
      <style jsx global>{`
        /* Light mode styles */
        .swagger-container {
          padding: 2rem;
          max-width: 1400px;
          margin: 0 auto;
        }

        /* Dark mode styles */
        .swagger-dark .swagger-ui {
          filter: invert(88%) hue-rotate(180deg);
        }

        .swagger-dark .swagger-ui .microlight {
          filter: invert(100%) hue-rotate(180deg);
        }

        /* Fix contrast issues in dark mode */
        .swagger-dark .swagger-ui .opblock-tag {
          border-color: #666;
        }

        .swagger-dark .swagger-ui .info {
          background-color: transparent;
        }

        /* Responsive tweaks */
        @media (max-width: 768px) {
          .swagger-container {
            padding: 1rem;
          }
        }

        /* Hide Swagger UI branding */
        .swagger-ui .info .title small {
          display: none;
        }

        /* Improve code block readability */
        .swagger-ui .highlight-code {
          font-size: 13px;
          line-height: 1.5;
        }

        /* Better button styling */
        .swagger-ui .btn {
          border-radius: 0.375rem;
          font-weight: 500;
        }

        /* Improve table styling */
        .swagger-ui table {
          border-collapse: collapse;
        }

        .swagger-ui table thead tr th {
          font-weight: 600;
          padding: 0.75rem;
        }

        .swagger-ui table tbody tr td {
          padding: 0.75rem;
        }
      `}</style>
    </div>
  );
}
