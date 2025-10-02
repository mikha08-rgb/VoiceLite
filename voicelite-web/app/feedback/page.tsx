import { Suspense } from 'react';
import FeedbackFormClient from './feedback-form-client';

// Disable prerendering for this page since it uses useSearchParams
export const dynamic = 'force-dynamic';

function LoadingFallback() {
  return (
    <main className="min-h-screen bg-stone-50 px-6 py-20 dark:bg-[#0f0f12]">
      <div className="mx-auto max-w-3xl">
        <div className="animate-pulse space-y-8">
          <div className="h-8 w-48 bg-stone-200 rounded dark:bg-stone-800"></div>
          <div className="h-12 w-96 bg-stone-200 rounded dark:bg-stone-800"></div>
          <div className="space-y-4">
            <div className="h-32 bg-stone-200 rounded-2xl dark:bg-stone-800"></div>
            <div className="h-12 bg-stone-200 rounded-xl dark:bg-stone-800"></div>
            <div className="h-64 bg-stone-200 rounded-xl dark:bg-stone-800"></div>
          </div>
        </div>
      </div>
    </main>
  );
}

export default function FeedbackPage() {
  return (
    <Suspense fallback={<LoadingFallback />}>
      <FeedbackFormClient />
    </Suspense>
  );
}
