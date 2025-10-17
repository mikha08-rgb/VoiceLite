import { MetadataRoute } from 'next';

/**
 * Sitemap Configuration for VoiceLite
 *
 * This file generates the XML sitemap for search engines.
 * Excluded routes:
 * - /basement-hustle-llc (confidential corporate info page)
 * - /api/* (API endpoints)
 * - /admin/* (admin dashboard)
 * - /checkout/* (payment flow)
 */
export default function sitemap(): MetadataRoute.Sitemap {
  const baseUrl = 'https://voicelite.app';

  return [
    {
      url: baseUrl,
      lastModified: new Date(),
      changeFrequency: 'weekly',
      priority: 1,
    },
    {
      url: `${baseUrl}/docs`,
      lastModified: new Date(),
      changeFrequency: 'monthly',
      priority: 0.8,
    },
    {
      url: `${baseUrl}/feedback`,
      lastModified: new Date(),
      changeFrequency: 'monthly',
      priority: 0.6,
    },
    {
      url: `${baseUrl}/terms`,
      lastModified: new Date(),
      changeFrequency: 'monthly',
      priority: 0.5,
    },
    {
      url: `${baseUrl}/privacy`,
      lastModified: new Date(),
      changeFrequency: 'monthly',
      priority: 0.5,
    },
    {
      url: `${baseUrl}/legal/refunds`,
      lastModified: new Date(),
      changeFrequency: 'monthly',
      priority: 0.5,
    },
  ];
}
