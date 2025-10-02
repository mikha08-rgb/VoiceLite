'use client';

import { useState, useTransition } from 'react';
import { useSearchParams } from 'next/navigation';
import { ArrowLeft, CheckCircle2, Send } from 'lucide-react';
import Link from 'next/link';
import { RippleButton } from '@/components/ripple-button';

const feedbackTypes = [
  { value: 'BUG', label: 'Bug Report', description: 'Something isn\'t working as expected' },
  { value: 'FEATURE_REQUEST', label: 'Feature Request', description: 'Suggest a new feature or improvement' },
  { value: 'QUESTION', label: 'Question', description: 'Ask about how VoiceLite works' },
  { value: 'GENERAL', label: 'General Feedback', description: 'Share your thoughts or suggestions' },
];

export default function FeedbackFormClient() {
  const searchParams = useSearchParams();
  const [type, setType] = useState<string>('GENERAL');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [email, setEmail] = useState('');
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  // Pre-fill metadata from URL params (desktop app integration)
  const appVersion = searchParams.get('version') || undefined;
  const osVersion = searchParams.get('os') || undefined;
  const source = searchParams.get('source') || 'web';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    startTransition(async () => {
      try {
        const response = await fetch('/api/feedback/submit', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            type,
            subject,
            message,
            email: email || undefined,
            metadata: {
              appVersion,
              osVersion,
              browser: typeof window !== 'undefined' ? navigator.userAgent : undefined,
              url: typeof window !== 'undefined' ? window.location.href : undefined,
            },
          }),
        });

        if (!response.ok) {
          const data = await response.json();
          throw new Error(data.error || 'Failed to submit feedback');
        }

        setIsSubmitted(true);
      } catch (err) {
        console.error('Feedback submission error:', err);
        setError(err instanceof Error ? err.message : 'Failed to submit feedback. Please try again.');
      }
    });
  };

  if (isSubmitted) {
    return (
      <main className="min-h-screen bg-stone-50 px-6 py-20 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-2xl">
          <div className="rounded-3xl border border-green-200 bg-gradient-to-br from-green-50 to-emerald-50 p-12 text-center shadow-lg dark:border-green-800 dark:from-green-950/50 dark:to-emerald-950/50">
            <CheckCircle2 className="mx-auto mb-6 h-16 w-16 text-green-600 dark:text-green-400" />
            <h1 className="mb-4 text-3xl font-bold text-stone-900 dark:text-stone-50">
              Thank You!
            </h1>
            <p className="mb-8 text-lg text-stone-700 dark:text-stone-300">
              Your feedback has been submitted successfully. We appreciate you taking the time to help improve VoiceLite!
            </p>
            {email && (
              <p className="mb-8 text-sm text-stone-600 dark:text-stone-400">
                We'll send a confirmation to <strong>{email}</strong> if we need more information.
              </p>
            )}
            <Link
              href="/"
              className="inline-flex items-center gap-2 rounded-full bg-purple-600 px-8 py-3 font-semibold text-white transition-transform hover:scale-105 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500"
            >
              <ArrowLeft className="h-5 w-5" />
              Back to Home
            </Link>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-stone-50 px-6 py-20 dark:bg-[#0f0f12]">
      <div className="mx-auto max-w-3xl">
        <Link
          href="/"
          className="mb-8 inline-flex items-center gap-2 text-sm font-medium text-purple-600 transition-colors hover:text-purple-700 dark:text-purple-400 dark:hover:text-purple-300"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Home
        </Link>

        <div className="mb-12 space-y-4">
          <h1 className="text-4xl font-bold text-stone-900 dark:text-stone-50">
            Send Feedback
          </h1>
          <p className="text-lg text-stone-600 dark:text-stone-400">
            Help us improve VoiceLite by sharing your thoughts, reporting bugs, or requesting features.
          </p>
          {source === 'desktop' && (
            <div className="rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-900 dark:border-blue-800 dark:bg-blue-950/50 dark:text-blue-100">
              <strong>Desktop App Detected:</strong> Version {appVersion || 'unknown'} • {osVersion || 'unknown OS'}
            </div>
          )}
        </div>

        <form onSubmit={handleSubmit} className="space-y-8">
          {/* Feedback Type */}
          <div className="space-y-4">
            <label className="block text-sm font-bold text-stone-900 dark:text-stone-50">
              What type of feedback are you sharing? *
            </label>
            <div className="grid gap-4 sm:grid-cols-2">
              {feedbackTypes.map((feedbackType) => (
                <button
                  key={feedbackType.value}
                  type="button"
                  onClick={() => setType(feedbackType.value)}
                  className={`rounded-2xl border p-4 text-left transition-all ${
                    type === feedbackType.value
                      ? 'border-purple-500 bg-purple-50 ring-2 ring-purple-500 dark:bg-purple-950/50'
                      : 'border-stone-200 bg-white hover:border-purple-200 dark:border-stone-800 dark:bg-stone-900 dark:hover:border-purple-800'
                  }`}
                >
                  <div className="mb-1 font-semibold text-stone-900 dark:text-stone-50">
                    {feedbackType.label}
                  </div>
                  <div className="text-xs text-stone-600 dark:text-stone-400">
                    {feedbackType.description}
                  </div>
                </button>
              ))}
            </div>
          </div>

          {/* Subject */}
          <div className="space-y-2">
            <label htmlFor="subject" className="block text-sm font-bold text-stone-900 dark:text-stone-50">
              Subject *
            </label>
            <input
              id="subject"
              type="text"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              required
              minLength={5}
              maxLength={200}
              placeholder="Brief summary of your feedback"
              className="w-full rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-900 placeholder-stone-400 focus:border-purple-500 focus:outline-none focus:ring-2 focus:ring-purple-500/20 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-50 dark:placeholder-stone-500"
            />
          </div>

          {/* Message */}
          <div className="space-y-2">
            <label htmlFor="message" className="block text-sm font-bold text-stone-900 dark:text-stone-50">
              Message *
            </label>
            <textarea
              id="message"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              required
              minLength={10}
              maxLength={5000}
              rows={8}
              placeholder="Provide detailed information about your feedback..."
              className="w-full rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-900 placeholder-stone-400 focus:border-purple-500 focus:outline-none focus:ring-2 focus:ring-purple-500/20 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-50 dark:placeholder-stone-500"
            />
            <p className="text-xs text-stone-500 dark:text-stone-400">
              {message.length}/5000 characters
            </p>
          </div>

          {/* Email (optional) */}
          <div className="space-y-2">
            <label htmlFor="email" className="block text-sm font-bold text-stone-900 dark:text-stone-50">
              Your Email (optional)
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="email@example.com"
              className="w-full rounded-xl border border-stone-300 bg-white px-4 py-3 text-stone-900 placeholder-stone-400 focus:border-purple-500 focus:outline-none focus:ring-2 focus:ring-purple-500/20 dark:border-stone-700 dark:bg-stone-900 dark:text-stone-50 dark:placeholder-stone-500"
            />
            <p className="text-xs text-stone-500 dark:text-stone-400">
              Provide your email if you'd like us to follow up with you.
            </p>
          </div>

          {/* Error message */}
          {error && (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-900 dark:border-red-800 dark:bg-red-950/50 dark:text-red-100">
              {error}
            </div>
          )}

          {/* Submit button */}
          <RippleButton
            type="submit"
            disabled={isPending}
            className="flex w-full items-center justify-center gap-2 rounded-full bg-gradient-to-br from-purple-600 to-violet-600 px-8 py-4 text-base font-semibold text-white shadow-lg shadow-purple-500/25 transition-all hover:scale-[1.02] hover:shadow-xl hover:shadow-purple-500/30 disabled:cursor-not-allowed disabled:opacity-50"
            rippleColor="rgba(255, 255, 255, 0.4)"
          >
            {isPending ? (
              <>Submitting...</>
            ) : (
              <>
                <Send className="h-5 w-5" />
                Submit Feedback
              </>
            )}
          </RippleButton>
        </form>
      </div>
    </main>
  );
}
