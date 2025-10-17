import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Business Information - VoiceLite',
  description: 'Legal business information for Basement Hustle LLC',
  robots: {
    index: true, // Allow Stripe to find this
    follow: true,
  },
};

export default function BusinessInfoPage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      <main className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <div className="bg-white dark:bg-gray-800 rounded-lg shadow-md border border-gray-200 dark:border-gray-700 p-8">
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-2">
            Business Information
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-400 mb-8">
            Legal entity details for VoiceLite
          </p>

          {/* Legal Entity */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Legal Entity
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Business Name:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">Basement Hustle LLC</dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Doing Business As:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">VoiceLite</dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Business Type:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">Limited Liability Company (LLC)</dd>
              </div>
            </dl>
          </section>

          {/* Contact & Address */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Contact Information
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Business Address:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <p>1315 Sherwood Rd</p>
                  <p>Glenview, IL 60025</p>
                  <p>United States</p>
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Support Email:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="mailto:basementhustleLLC@gmail.com" className="text-blue-600 dark:text-blue-400 hover:underline">
                    basementhustleLLC@gmail.com
                  </a>
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Business Email:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="mailto:basementhustleLLC@gmail.com" className="text-blue-600 dark:text-blue-400 hover:underline">
                    basementhustleLLC@gmail.com
                  </a>
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Phone:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="tel:+18476120901" className="text-blue-600 dark:text-blue-400 hover:underline">
                    +1-847-612-0901
                  </a>
                </dd>
              </div>
            </dl>
          </section>

          {/* Product Information */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Product & Services
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Product Name:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">VoiceLite</dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Description:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  Offline speech-to-text desktop application for Windows. Converts voice to text instantly with 100% local processing.
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Website:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="https://voicelite.app" className="text-blue-600 dark:text-blue-400 hover:underline">
                    https://voicelite.app
                  </a>
                </dd>
              </div>
            </dl>
          </section>

          {/* Pricing */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Pricing & Currency
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Currency:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">USD (United States Dollar)</dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Pricing:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <ul className="list-disc list-inside space-y-1">
                    <li>Free Tier: $0 (limited features)</li>
                    <li>Pro Version: $20 USD one-time payment</li>
                  </ul>
                </dd>
              </div>
            </dl>
          </section>

          {/* Payment & Security */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Payment & Security
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Payment Processor:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">Stripe (PCI-DSS Level 1 Certified)</dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Security:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  All payments processed securely via Stripe. Site uses HTTPS encryption.
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Delivery:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  Digital delivery - License key sent via email immediately upon purchase
                </dd>
              </div>
            </dl>
          </section>

          {/* Policies */}
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 dark:text-white mb-4 pb-2 border-b border-gray-200 dark:border-gray-700">
              Policies
            </h2>
            <dl className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Refund Policy:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="/legal/refunds" className="text-blue-600 dark:text-blue-400 hover:underline">
                    30-day money-back guarantee
                  </a>
                  {' '}- Full refund available within 30 days of purchase
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Cancellation:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="/terms" className="text-blue-600 dark:text-blue-400 hover:underline">
                    Subscription cancellation available anytime
                  </a>
                  {' '}- Details in Terms of Service (Section 4.1)
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Terms of Service:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="/terms" className="text-blue-600 dark:text-blue-400 hover:underline">
                    View full terms
                  </a>
                </dd>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <dt className="text-sm font-medium text-gray-700 dark:text-gray-300">Privacy Policy:</dt>
                <dd className="sm:col-span-2 text-sm text-gray-900 dark:text-white">
                  <a href="/privacy" className="text-blue-600 dark:text-blue-400 hover:underline">
                    View privacy policy
                  </a>
                  {' '}- GDPR compliant, 100% offline voice processing
                </dd>
              </div>
            </dl>
          </section>

          {/* Footer Note */}
          <div className="mt-8 p-4 bg-blue-50 dark:bg-blue-950 border border-blue-200 dark:border-blue-800 rounded-lg">
            <p className="text-sm text-blue-900 dark:text-blue-100">
              <strong>Note:</strong> This page provides business information for payment processor verification and transparency purposes.
              For customer support, please visit our <a href="/feedback" className="underline hover:text-blue-700 dark:hover:text-blue-300">support page</a> or email{' '}
              <a href="mailto:basementhustleLLC@gmail.com" className="underline hover:text-blue-700 dark:hover:text-blue-300">basementhustleLLC@gmail.com</a>.
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}