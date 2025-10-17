import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Refund Policy - VoiceLite',
  description: '30-day money-back guarantee. Full refund policy for VoiceLite software licenses.',
  openGraph: {
    title: 'Refund Policy - VoiceLite',
    description: '30-day money-back guarantee. Full refund policy for VoiceLite software licenses.',
  },
};

export default function RefundPolicyPage() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-50 to-white dark:from-gray-950 dark:to-gray-900">
      <main className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <article className="prose prose-gray dark:prose-invert max-w-none">
          <h1>Refund Policy</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            Last Updated: <strong>January 2025</strong>
          </p>

          <section>
            <h2>30-Day Money-Back Guarantee</h2>
            <p>
              We stand behind VoiceLite with a <strong>30-day money-back guarantee</strong>. If you're not completely satisfied with your purchase, you can request a full refund within 30 days of your first payment—no questions asked.
            </p>
          </section>

          <section>
            <h2>Eligible Purchases</h2>
            <p>Our refund policy applies to all paid VoiceLite license tiers:</p>
            <ul>
              <li><strong>Personal License</strong> - $29.99</li>
              <li><strong>Professional License</strong> - $59.99</li>
              <li><strong>Business License</strong> - $199.99</li>
            </ul>
            <p>
              Both subscription and lifetime licenses are eligible for refunds within the 30-day window.
            </p>
          </section>

          <section>
            <h2>How to Request a Refund</h2>
            <p>To request a refund, please follow these steps:</p>
            <ol>
              <li>
                <strong>Email our support team</strong> at{' '}
                <a href="mailto:support@voicelite.app">support@voicelite.app</a>
              </li>
              <li>
                Include your <strong>license key</strong> or the <strong>email address</strong> used for purchase
              </li>
              <li>
                Optionally, let us know why you're requesting a refund (helps us improve!)
              </li>
            </ol>
            <p>
              We'll process your request within <strong>1-2 business days</strong>.
            </p>
          </section>

          <section>
            <h2>Refund Processing Time</h2>
            <p>
              Once your refund is approved, it will be processed back to your original payment method. Please allow:
            </p>
            <ul>
              <li><strong>5-10 business days</strong> for the refund to appear in your account</li>
              <li>
                Exact timing depends on your bank or credit card provider
              </li>
            </ul>
          </section>

          <section>
            <h2>After Your Refund</h2>
            <p>When a refund is issued:</p>
            <ul>
              <li>Your license key will be <strong>deactivated</strong></li>
              <li>You will lose access to VoiceLite Pro features</li>
              <li>
                You can continue using the <strong>free trial version</strong> if you haven't already exhausted the 14-day trial period
              </li>
            </ul>
          </section>

          <section>
            <h2>Subscription Cancellations</h2>
            <p>
              If you have a <strong>subscription license</strong> and simply want to cancel future billing (without requesting a refund):
            </p>
            <ul>
              <li>
                Log in to your <a href="https://voicelite.app/admin">account dashboard</a>
              </li>
              <li>Navigate to <strong>Billing</strong> and click <strong>Cancel Subscription</strong></li>
              <li>
                You'll retain access to Pro features until the end of your current billing period
              </li>
            </ul>
            <p>
              Alternatively, email <a href="mailto:support@voicelite.app">support@voicelite.app</a> and we'll cancel it for you.
            </p>
          </section>

          <section>
            <h2>Exceptions</h2>
            <p>Our refund policy has the following limitations:</p>
            <ul>
              <li>
                Refunds are only available within <strong>30 days of first purchase</strong>
              </li>
              <li>
                Refunds are not available for <strong>chargebacks</strong>—please contact us first
              </li>
              <li>
                Fraudulent or abusive refund requests may be declined
              </li>
            </ul>
          </section>

          <section>
            <h2>Questions?</h2>
            <p>
              If you have any questions about our refund policy, please reach out to us at{' '}
              <a href="mailto:support@voicelite.app">support@voicelite.app</a>. We're here to help!
            </p>
          </section>

          <section className="bg-blue-50 dark:bg-blue-950 p-6 rounded-lg border border-blue-100 dark:border-blue-900 mt-8">
            <h3 className="text-blue-900 dark:text-blue-100 mt-0">Still Not Sure?</h3>
            <p className="text-blue-800 dark:text-blue-200 mb-0">
              Try VoiceLite <strong>free for 14 days</strong> with our trial version—no credit card required. Experience all the features before committing to a purchase.
            </p>
          </section>
        </article>
      </main>
    </div>
  );
}
