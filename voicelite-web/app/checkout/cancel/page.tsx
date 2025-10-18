import Link from 'next/link';

export default function CheckoutCancelPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-50 dark:bg-stone-950 px-4">
      <div className="max-w-md w-full text-center space-y-6 p-8 bg-white dark:bg-stone-900 rounded-lg shadow-lg">
        <div className="text-yellow-600 dark:text-yellow-400 text-6xl">✋</div>

        <h1 className="text-3xl font-bold text-stone-900 dark:text-stone-100">
          Payment Cancelled
        </h1>

        <div className="space-y-4 text-stone-600 dark:text-stone-400">
          <p className="text-lg">
            No worries! Your payment was cancelled.
          </p>

          <div className="bg-stone-100 dark:bg-stone-800 p-4 rounded-lg space-y-2">
            <p className="font-semibold text-stone-900 dark:text-stone-100">
              💡 Try the Free Version
            </p>
            <p className="text-sm">
              You can still use VoiceLite with the Tiny model (included in the free download).
              Upgrade to Pro anytime for $20 to unlock all 4 advanced models (90-98% accuracy).
            </p>
          </div>
        </div>

        <div className="flex flex-col sm:flex-row gap-3 pt-4">
          <Link
            href="/"
            className="flex-1 inline-flex items-center justify-center px-6 py-3 border border-stone-300 dark:border-stone-700 rounded-lg text-stone-700 dark:text-stone-300 hover:bg-stone-50 dark:hover:bg-stone-800 transition-colors"
          >
            ← Back to Home
          </Link>
          <a
            href="https://github.com/mikha08-rgb/VoiceLite/releases/latest"
            target="_blank"
            rel="noopener noreferrer"
            className="flex-1 inline-flex items-center justify-center px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            Download Free Version
          </a>
        </div>
      </div>
    </div>
  );
}
