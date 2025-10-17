import { MetadataRoute } from 'next';

/**
 * Robots.txt Configuration for VoiceLite
 *
 * This file controls search engine crawler behavior.
 * Explicitly disallows confidential and internal routes.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: '*',
        allow: [
          '/',
          '/docs',
          '/feedback',
          '/terms',
          '/privacy',
          '/legal/refunds',
        ],
        disallow: [
          '/api/',
          '/admin/',
          '/checkout/',
          '/basement-hustle-llc', // Confidential corporate info page
        ],
      },
    ],
    sitemap: 'https://voicelite.app/sitemap.xml',
  };
}
