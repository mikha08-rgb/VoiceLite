/**
 * Basement Hustle LLC - Corporate Entity Information Page
 *
 * This page is provided exclusively for payment processor (Stripe) verification.
 * It is intentionally not linked from anywhere on the site and excluded from SEO indexing.
 *
 * CONFIGURATION:
 *
 * 1. Secret Key Protection (Optional, disabled by default):
 *    - Set environment variable: PRIVATE_PAGE_KEY=your-secret-key
 *    - Access page with: /basement-hustle-llc?k=your-secret-key
 *    - Without the correct key, page shows 404
 *    - To disable: remove PRIVATE_PAGE_KEY from environment
 *
 * 2. Updating Content:
 *    - Replace all TODO placeholders with real information
 *    - Update LAST_UPDATED constant when making changes
 *    - Mask sensitive PII but indicate "available to Stripe on request"
 *
 * 3. Verification:
 *    - Check noindex: View source and look for <meta name="robots" content="noindex,nofollow">
 *    - Check sitemap: Visit /sitemap.xml and verify this route is NOT listed
 *    - Check robots.txt: Visit /robots.txt and verify Disallow: /basement-hustle-llc
 *
 * SECURITY:
 * - Never commit real PII to git
 * - Use environment variables for sensitive data if needed
 * - Keep this page URL confidential
 */

import { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { headers } from 'next/headers';

// Last updated timestamp (update when content changes)
const LAST_UPDATED = '2025-01-16T00:00:00Z';

// Page metadata with noindex directive
export const metadata: Metadata = {
  title: 'Basement Hustle LLC - Corporate Information',
  description: 'Corporate entity information for payment processor verification.',
  robots: {
    index: false,
    follow: false,
    googleBot: {
      index: false,
      follow: false,
    },
  },
};

// Type definitions for better structure
interface EntityInfo {
  label: string;
  value: string;
  note?: string;
}

interface Section {
  title: string;
  items: EntityInfo[];
}

// Corporate entity information structure
const entityData: Section[] = [
  {
    title: 'Legal Entity',
    items: [
      { label: 'Legal Business Name', value: 'Basement Hustle LLC' },
      { label: 'DBA / Public Name', value: 'VoiceLite' },
      { label: 'Business Type', value: 'Limited Liability Company (LLC), Single-Member' },
      {
        label: 'EIN / Tax ID',
        value: 'XX-XXXXXXX',
        note: 'Full EIN available to Stripe upon request'
      },
    ],
  },
  {
    title: 'Business Address',
    items: [
      {
        label: 'Registered Business Address',
        value: 'TODO: Street Address, City, State ZIP, Country'
      },
      {
        label: 'Mailing Address',
        value: 'Same as registered address'
      },
    ],
  },
  {
    title: 'Incorporation Details',
    items: [
      { label: 'Country of Incorporation', value: 'TODO: United States' },
      { label: 'State/Province', value: 'TODO: State' },
      { label: 'Formation Date', value: 'TODO: YYYY-MM-DD' },
    ],
  },
  {
    title: 'Online Presence',
    items: [
      { label: 'Primary Website', value: 'https://voicelite.app' },
      { label: 'Product Documentation', value: 'https://voicelite.app/docs' },
      { label: 'Support Portal', value: 'https://voicelite.app/feedback' },
    ],
  },
  {
    title: 'Business Description',
    items: [
      {
        label: 'Industry',
        value: 'Software / Productivity Tools'
      },
      {
        label: 'Product/Service Offering',
        value: 'VoiceLite is a desktop speech-to-text application for Windows. We sell software licenses (subscription and lifetime) that enable offline voice transcription using AI. No voice data is transmitted to cloud servers.'
      },
      {
        label: 'Business Model',
        value: 'Freemium SaaS - Free 14-day trial, paid tiers: Personal ($29.99), Professional ($59.99), Business ($199.99)'
      },
    ],
  },
  {
    title: 'Contact Information',
    items: [
      { label: 'Support Email', value: 'support@voicelite.app' },
      { label: 'Business Email', value: 'contact@voicelite.app' },
      { label: 'Support Phone', value: 'TODO: +1-XXX-XXX-XXXX' },
      { label: 'Contact Form', value: 'https://voicelite.app/feedback' },
    ],
  },
  {
    title: 'Legal & Compliance',
    items: [
      { label: 'Terms of Service', value: 'https://voicelite.app/terms' },
      { label: 'Privacy Policy', value: 'https://voicelite.app/privacy' },
      { label: 'Refund Policy', value: 'https://voicelite.app/legal/refunds' },
    ],
  },
  {
    title: 'Ownership & Control',
    items: [
      {
        label: 'Beneficial Owner(s)',
        value: '[Name Redacted]',
        note: 'Full name, DOB, and address available to Stripe upon request'
      },
      {
        label: 'Ownership Percentage',
        value: '100% (Single-member LLC)'
      },
      {
        label: 'Authorized Representative',
        value: '[Name Redacted], Owner/Operator',
        note: 'Full details available to Stripe upon request'
      },
    ],
  },
  {
    title: 'Banking & Settlement',
    items: [
      {
        label: 'Bank Name',
        value: 'TODO: Bank Name'
      },
      {
        label: 'Account Information',
        value: 'Account ending in XXXX',
        note: 'Full account details available to Stripe upon request'
      },
      {
        label: 'Currency',
        value: 'USD (United States Dollar)'
      },
    ],
  },
  {
    title: 'Customer & Market',
    items: [
      {
        label: 'Customer Geography',
        value: 'Global - primarily United States, Canada, United Kingdom, European Union, Australia'
      },
      {
        label: 'Target Market',
        value: 'Professionals, writers, developers, accessibility users requiring offline speech-to-text'
      },
      {
        label: 'Average Transaction Size',
        value: '$29.99 - $199.99'
      },
    ],
  },
  {
    title: 'Fulfillment & Delivery',
    items: [
      {
        label: 'Delivery Method',
        value: 'Digital download - Software license keys delivered via email immediately upon purchase'
      },
      {
        label: 'Delivery Timeframe',
        value: 'Instant (within minutes of payment confirmation)'
      },
      {
        label: 'Product Access',
        value: 'Customers download installer from website and activate with license key'
      },
      {
        label: 'Software Type',
        value: 'Desktop application (Windows .exe installer), 100% offline operation after installation'
      },
    ],
  },
  {
    title: 'Refund & Return Policy',
    items: [
      {
        label: 'Refund Window',
        value: '30 days from date of first purchase'
      },
      {
        label: 'Refund Terms',
        value: 'Full refund available within 30 days, no questions asked. Contact support@voicelite.app to request.'
      },
      {
        label: 'Processing Time',
        value: '5-10 business days for refund to appear in customer account'
      },
      {
        label: 'Full Policy',
        value: 'https://voicelite.app/legal/refunds'
      },
    ],
  },
  {
    title: 'Risk & Disputes',
    items: [
      {
        label: 'Chargeback History',
        value: 'TODO: None / Minimal (update after launch)'
      },
      {
        label: 'Fraud Prevention',
        value: 'Hardware-bound licensing (CPU + Motherboard fingerprint), Stripe Radar fraud detection'
      },
      {
        label: 'Disputes Contact',
        value: 'support@voicelite.app (escalate to contact@voicelite.app)'
      },
      {
        label: 'Expected Monthly Volume',
        value: 'TODO: $X,XXX - $XX,XXX (update after launch)'
      },
    ],
  },
  {
    title: 'Data & Privacy',
    items: [
      {
        label: 'Data Processing',
        value: 'All voice data processed 100% locally on user device. Zero cloud uploads.'
      },
      {
        label: 'Personal Data Collected',
        value: 'Email address, payment information (via Stripe), license activation metadata (hardware fingerprint)'
      },
      {
        label: 'Data Retention',
        value: '7 years (tax compliance), deletable upon request per GDPR Article 17'
      },
      {
        label: 'Compliance',
        value: 'GDPR compliant, PCI-DSS Level 1 (via Stripe)'
      },
    ],
  },
];

async function BasementHustleLLCPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  // Secret key protection (optional, disabled by default)
  const params = await searchParams;
  const PRIVATE_PAGE_KEY = process.env.PRIVATE_PAGE_KEY;

  if (PRIVATE_PAGE_KEY) {
    const providedKey = params.k;
    if (providedKey !== PRIVATE_PAGE_KEY) {
      notFound(); // Show 404 if wrong/missing key
    }
  }

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      {/* Verification Banner */}
      <div className="bg-blue-50 dark:bg-blue-950 border-b border-blue-100 dark:border-blue-900">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-3">
          <div className="flex items-center gap-2 text-sm text-blue-800 dark:text-blue-200">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-5 w-5"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path
                fillRule="evenodd"
                d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
                clipRule="evenodd"
              />
            </svg>
            <span>
              <strong>Confidential:</strong> This page is provided for payment processor verification. Not publicly listed.
            </span>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <main className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        {/* Header */}
        <div className="mb-12">
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-2">
            Basement Hustle LLC
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            Corporate Entity Information for Payment Processor Verification
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-500 mt-4">
            Last updated: <time dateTime={LAST_UPDATED}>{new Date(LAST_UPDATED).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })}</time>
          </p>
        </div>

        {/* Information Sections */}
        <div className="space-y-12">
          {entityData.map((section) => (
            <section key={section.title} className="bg-white dark:bg-gray-800 rounded-lg shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-6 pb-3 border-b border-gray-200 dark:border-gray-700">
                {section.title}
              </h2>
              <dl className="grid grid-cols-1 gap-6">
                {section.items.map((item) => (
                  <div key={item.label} className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                    <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">
                      {item.label}
                    </dt>
                    <dd className="sm:col-span-2">
                      <div className="text-sm text-gray-900 dark:text-white">
                        {item.value.startsWith('http') ? (
                          <a
                            href={item.value}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-blue-600 dark:text-blue-400 hover:underline"
                          >
                            {item.value}
                          </a>
                        ) : (
                          item.value
                        )}
                      </div>
                      {item.note && (
                        <p className="text-xs text-gray-500 dark:text-gray-400 mt-1 italic">
                          {item.note}
                        </p>
                      )}
                    </dd>
                  </div>
                ))}
              </dl>
            </section>
          ))}
        </div>

        {/* Footer Notice */}
        <div className="mt-12 p-6 bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
            Notice to Payment Processors
          </h3>
          <p className="text-sm text-gray-700 dark:text-gray-300 leading-relaxed">
            This information is provided in good faith for the purpose of identity verification,
            risk assessment, and compliance review by authorized payment processors and financial
            institutions. Any redacted information (marked as "[Name Redacted]" or "XX-XXXX")
            is available upon direct request through official verification channels. For additional
            documentation or clarification, please contact <a href="mailto:contact@voicelite.app" className="text-blue-600 dark:text-blue-400 hover:underline">contact@voicelite.app</a>.
          </p>
        </div>

        {/* Version Info for Debugging */}
        {process.env.NODE_ENV === 'development' && (
          <div className="mt-8 p-4 bg-yellow-50 dark:bg-yellow-950 border border-yellow-200 dark:border-yellow-800 rounded text-xs text-yellow-800 dark:text-yellow-200">
            <strong>Dev Mode:</strong> Secret key protection is {PRIVATE_PAGE_KEY ? 'ENABLED' : 'DISABLED'}
            {PRIVATE_PAGE_KEY && ' (access with ?k=YOUR_KEY)'}
          </div>
        )}
      </main>
    </div>
  );
}

export default BasementHustleLLCPage;
