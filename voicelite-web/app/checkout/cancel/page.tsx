'use client';

import Link from 'next/link';

export default function CheckoutCancelPage() {
  return (
    <main className="min-h-screen bg-gradient-to-br from-stone-50 to-stone-100 dark:from-stone-950 dark:to-stone-900 flex items-center justify-center px-6">
      <div className="max-w-2xl w-full bg-white dark:bg-stone-900 rounded-2xl shadow-2xl p-12 text-center">
        {/* Cancel Icon */}
        <div className="mx-auto w-20 h-20 bg-stone-100 dark:bg-stone-800 rounded-full flex items-center justify-center mb-6">
          <svg className="w-12 h-12 text-stone-600 dark:text-stone-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </div>

        {/* Title */}
        <h1 className="text-4xl font-bold text-stone-900 dark:text-stone-50 mb-4">
          Checkout Cancelled
        </h1>

        {/* Description */}
        <p className="text-xl text-stone-600 dark:text-stone-400 mb-8">
          Your payment was cancelled. No charges were made.
        </p>

        {/* Info Box */}
        <div className="bg-blue-50 dark:bg-blue-950/50 border border-blue-200 dark:border-blue-800 rounded-xl p-6 mb-8 text-left">
          <p className="text-stone-700 dark:text-stone-300">
            If you experienced any issues during checkout or have questions, please contact us at{' '}
            <a href="mailto:support@voicelite.app" className="text-blue-600 dark:text-blue-400 underline hover:no-underline">
              support@voicelite.app
            </a>
          </p>
        </div>

        {/* Action Buttons */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <Link
            href="/#pricing"
            className="inline-flex items-center justify-center px-8 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 transition-colors"
          >
            Try Again
          </Link>
          <Link
            href="/"
            className="inline-flex items-center justify-center px-8 py-3 border-2 border-stone-300 dark:border-stone-700 text-stone-700 dark:text-stone-300 font-semibold rounded-lg hover:border-blue-600 hover:text-blue-600 dark:hover:border-blue-400 dark:hover:text-blue-400 transition-colors"
          >
            Back to Home
          </Link>
        </div>
      </div>
    </main>
  );
}
