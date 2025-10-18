import Link from 'next/link';

export default function CheckoutSuccessPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-50 dark:bg-stone-950 px-4">
      <div className="max-w-md w-full text-center space-y-6 p-8 bg-white dark:bg-stone-900 rounded-lg shadow-lg">
        <div className="text-green-600 dark:text-green-400 text-6xl">✓</div>

        <h1 className="text-3xl font-bold text-stone-900 dark:text-stone-100">
          Payment Successful!
        </h1>

        <div className="space-y-4 text-stone-600 dark:text-stone-400">
          <p className="text-lg">
            Thank you for supporting VoiceLite development!
          </p>

          <div className="bg-stone-100 dark:bg-stone-800 p-4 rounded-lg space-y-2">
            <p className="font-semibold text-stone-900 dark:text-stone-100">
              📧 Check your email
            </p>
            <p className="text-sm">
              Your license key has been sent to your email address.
            </p>
          </div>

          <div className="text-left space-y-2 pt-4">
            <p className="font-semibold text-stone-900 dark:text-stone-100">Next steps:</p>
            <ol className="text-sm space-y-1 list-decimal list-inside">
              <li>Check your email for the license key</li>
              <li>Download VoiceLite if you haven't already</li>
              <li>Launch VoiceLite - you'll see the activation dialog</li>
              <li>Enter your license key to unlock all Pro models</li>
            </ol>
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
            Download VoiceLite
          </a>
        </div>
      </div>
    </div>
  );
}
