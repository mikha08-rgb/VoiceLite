'use client';

import { useState } from 'react';
import Link from 'next/link';

export default function RetrieveLicensePage() {
  const [email, setEmail] = useState('');
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [message, setMessage] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setStatus('loading');
    setMessage('');

    try {
      const response = await fetch('/api/licenses/retrieve', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      });

      const data = await response.json();

      if (response.ok) {
        setStatus('success');
        setMessage(data.message || 'If a license exists for this email, you will receive it shortly.');
      } else {
        setStatus('error');
        setMessage(data.error || 'Something went wrong. Please try again.');
      }
    } catch (err) {
      setStatus('error');
      setMessage('Unable to connect. Please try again.');
    }
  };

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-50 dark:from-blue-950 dark:to-indigo-950 flex items-center justify-center px-6">
      <div className="max-w-md w-full bg-white dark:bg-stone-900 rounded-2xl shadow-2xl p-8">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="mx-auto w-16 h-16 bg-blue-100 dark:bg-blue-900/30 rounded-full flex items-center justify-center mb-4">
            <svg className="w-8 h-8 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" />
            </svg>
          </div>
          <h1 className="text-2xl font-bold text-stone-900 dark:text-stone-50 mb-2">
            Retrieve Your License
          </h1>
          <p className="text-stone-600 dark:text-stone-400">
            Enter the email you used to purchase VoiceLite Pro
          </p>
        </div>

        {status === 'success' ? (
          <div className="text-center">
            <div className="mx-auto w-16 h-16 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center mb-4">
              <svg className="w-8 h-8 text-green-600 dark:text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h2 className="text-lg font-semibold text-stone-900 dark:text-stone-50 mb-2">
              Check Your Email
            </h2>
            <p className="text-stone-600 dark:text-stone-400 mb-6">
              {message}
            </p>
            <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4 mb-6 text-left">
              <p className="text-sm text-yellow-800 dark:text-yellow-200">
                <strong>Tip:</strong> Check your spam/junk folder if you don't see it within 2 minutes.
              </p>
            </div>
            <button
              onClick={() => {
                setStatus('idle');
                setEmail('');
              }}
              className="text-blue-600 dark:text-blue-400 hover:underline"
            >
              Try a different email
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-6">
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                Email Address
              </label>
              <input
                type="email"
                id="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                required
                disabled={status === 'loading'}
                className="w-full px-4 py-3 rounded-lg border border-stone-300 dark:border-stone-700 bg-white dark:bg-stone-800 text-stone-900 dark:text-stone-50 focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50"
              />
            </div>

            {status === 'error' && (
              <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3">
                <p className="text-sm text-red-800 dark:text-red-200">{message}</p>
              </div>
            )}

            <button
              type="submit"
              disabled={status === 'loading' || !email}
              className="w-full py-3 px-4 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 focus:ring-4 focus:ring-blue-300 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {status === 'loading' ? (
                <span className="flex items-center justify-center gap-2">
                  <svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  Sending...
                </span>
              ) : (
                'Send License Key'
              )}
            </button>
          </form>
        )}

        {/* Footer */}
        <div className="mt-8 pt-6 border-t border-stone-200 dark:border-stone-700 text-center">
          <p className="text-sm text-stone-500 dark:text-stone-400">
            Need help?{' '}
            <a href="mailto:BasmentHustleLLC@gmail.com" className="text-blue-600 dark:text-blue-400 hover:underline">
              Contact support
            </a>
          </p>
          <Link href="/" className="inline-block mt-4 text-sm text-stone-500 dark:text-stone-400 hover:text-blue-600 dark:hover:text-blue-400">
            ← Back to Home
          </Link>
        </div>
      </div>
    </main>
  );
}
