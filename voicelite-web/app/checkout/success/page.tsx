'use client';

import { Suspense, useEffect } from 'react';
import { useSearchParams } from 'next/navigation';
import Link from 'next/link';

const CURRENT_VERSION = process.env.NEXT_PUBLIC_CURRENT_VERSION || '1.4.0.0';

function SuccessContent() {
  const searchParams = useSearchParams();
  const sessionId = searchParams.get('session_id');

  useEffect(() => {
    // Optional: Track successful purchase
    if (sessionId) {
      console.log('Purchase successful:', sessionId);
    }
  }, [sessionId]);

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-950 dark:to-indigo-950 flex items-center justify-center px-6">
      <div className="max-w-2xl w-full bg-white dark:bg-stone-900 rounded-2xl shadow-2xl p-12 text-center">
        {/* Success Icon */}
        <div className="mx-auto w-20 h-20 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center mb-6">
          <svg className="w-12 h-12 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
        </div>

        {/* Title */}
        <h1 className="text-4xl font-bold text-stone-900 dark:text-stone-50 mb-4">
          Payment Successful! 🎉
        </h1>

        {/* Description */}
        <p className="text-xl text-stone-600 dark:text-stone-400 mb-8">
          Thank you for purchasing VoiceLite Pro!
        </p>

        {/* Instructions */}
        <div className="bg-blue-50 dark:bg-blue-950/50 border border-blue-200 dark:border-blue-800 rounded-xl p-6 mb-8 text-left">
          <h2 className="text-lg font-semibold text-stone-900 dark:text-stone-50 mb-4">
            📧 Next Steps:
          </h2>
          <ol className="space-y-3 text-stone-700 dark:text-stone-300">
            <li className="flex items-start gap-3">
              <span className="flex-shrink-0 w-6 h-6 bg-blue-600 text-white rounded-full flex items-center justify-center text-sm font-bold">1</span>
              <span><strong>Check your email</strong> for your license key (should arrive within 1-2 minutes)</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="flex-shrink-0 w-6 h-6 bg-blue-600 text-white rounded-full flex items-center justify-center text-sm font-bold">2</span>
              <span><strong>Download VoiceLite</strong> if you haven't already</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="flex-shrink-0 w-6 h-6 bg-blue-600 text-white rounded-full flex items-center justify-center text-sm font-bold">3</span>
              <span><strong>Open Settings → License</strong> in the app and paste your license key</span>
            </li>
            <li className="flex items-start gap-3">
              <span className="flex-shrink-0 w-6 h-6 bg-blue-600 text-white rounded-full flex items-center justify-center text-sm font-bold">4</span>
              <span><strong>Enjoy all Pro features!</strong> 🚀</span>
            </li>
          </ol>
        </div>

        {/* Email Not Received */}
        <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-xl p-4 mb-8 text-left">
          <p className="text-sm text-yellow-800 dark:text-yellow-200">
            <strong>Don't see the email?</strong> Check your spam folder or{' '}
            <Link href="/retrieve" className="underline hover:no-underline font-semibold">
              retrieve your license key here
            </Link>
          </p>
        </div>

        {/* Action Buttons */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <a
            href={`/api/download?version=${CURRENT_VERSION}`}
            className="inline-flex items-center justify-center gap-2 px-8 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            Download VoiceLite
          </a>
          <Link
            href="/"
            className="inline-flex items-center justify-center px-8 py-3 border-2 border-stone-300 dark:border-stone-700 text-stone-700 dark:text-stone-300 font-semibold rounded-lg hover:border-blue-600 hover:text-blue-600 dark:hover:border-blue-400 dark:hover:text-blue-400 transition-colors"
          >
            Back to Home
          </Link>
        </div>

        {/* Order Reference */}
        {sessionId && (
          <p className="mt-8 text-xs text-stone-400 dark:text-stone-600">
            Order ID: {sessionId}
          </p>
        )}
      </div>
    </main>
  );
}

export default function CheckoutSuccessPage() {
  return (
    <Suspense fallback={
      <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-950 dark:to-indigo-950 flex items-center justify-center">
        <div className="text-stone-600 dark:text-stone-400">Loading...</div>
      </div>
    }>
      <SuccessContent />
    </Suspense>
  );
}
