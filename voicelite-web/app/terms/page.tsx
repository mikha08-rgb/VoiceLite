import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Terms of Service - VoiceLite",
  description: "VoiceLite terms of service: Subscription details, refund policy, and user agreement for our offline voice typing software.",
};

export default function TermsOfService() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-6">
      <div className="max-w-4xl mx-auto bg-white rounded-lg shadow-md p-8">
        <h1 className="text-4xl font-bold text-gray-900 mb-6">Terms of Service</h1>
        <p className="text-sm text-gray-500 mb-8">Last Updated: January 2025</p>

        <div className="prose prose-gray max-w-none">
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">1. Agreement to Terms</h2>
            <p className="text-gray-700 mb-4">
              By downloading, installing, or using VoiceLite ("the Software"), you agree to be bound by these
              Terms of Service ("Terms"). If you do not agree to these Terms, do not use the Software.
            </p>
            <p className="text-gray-700">
              These Terms constitute a legally binding agreement between you ("User," "you," or "your") and
              VoiceLite ("we," "us," or "our").
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">2. License Grant</h2>
            <p className="text-gray-700 mb-4">
              Subject to your compliance with these Terms, we grant you a limited, non-exclusive,
              non-transferable, revocable license to:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Install and use VoiceLite on devices you own or control</li>
              <li>Use the Software for personal or commercial purposes</li>
            </ul>
            <p className="text-gray-700">
              The Software's source code is licensed under the MIT License (see{' '}
              <a
                href="https://github.com/mikha08-rgb/VoiceLite/blob/master/LICENSE"
                className="text-blue-600 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                LICENSE
              </a>
              ). Certain features (Pro models) require a paid subscription.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">3. Subscription Plans</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.1 Free Version</h3>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Access to Tiny AI model only</li>
              <li>Unlimited usage</li>
              <li>100% offline functionality</li>
              <li>No credit card required</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.2 Pro Version ($20 / 3 months or $99 one-time)</h3>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Access to all 5 AI models (Tiny, Base, Small, Medium, Large)</li>
              <li>Unlimited usage</li>
              <li>Priority email support</li>
              <li>Offline functionality (license validation requires one-time internet connection)</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.3 Billing</h3>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Subscriptions are billed monthly via Stripe</li>
              <li>Automatic renewal on the same day each month unless canceled</li>
              <li>Prices are in USD and subject to change with 30 days notice</li>
              <li>You are responsible for all taxes applicable in your jurisdiction</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">4. Cancellation and Refunds</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.1 Cancellation</h3>
            <p className="text-gray-700 mb-4">
              You may cancel your Pro subscription at any time through the Stripe customer portal. Upon
              cancellation:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>You retain Pro access until the end of your current billing period</li>
              <li>No further charges will be made</li>
              <li>The Software continues to work offline with Free tier features</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.2 Refund Policy</h3>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>7-Day Money-Back Guarantee:</strong> Full refund if requested within 7 days of first purchase</li>
              <li>Refunds are not provided for partial months</li>
              <li>Refund requests must be sent to{' '}
                <a href="mailto:contact@voicelite.app" className="text-blue-600 hover:underline">
                  contact@voicelite.app
                </a>
              </li>
              <li>Refunds are processed within 5-10 business days</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.3 Termination by Us</h3>
            <p className="text-gray-700">
              We reserve the right to suspend or terminate your Pro subscription if you violate these Terms,
              with a pro-rated refund for unused time.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">5. Acceptable Use</h2>
            <p className="text-gray-700 mb-4">You agree NOT to:</p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Reverse engineer, decompile, or disassemble the Software (except as permitted by applicable law or the MIT License)</li>
              <li>Share your Pro license key with others</li>
              <li>Use the Software for illegal purposes or to violate others' rights</li>
              <li>Attempt to bypass license validation mechanisms</li>
              <li>Use the Software to create competing products without proper attribution (as required by MIT License)</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">6. Intellectual Property</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite is open source software licensed under the MIT License. The source code is available at{' '}
              <a
                href="https://github.com/mikha08-rgb/VoiceLite"
                className="text-blue-600 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub
              </a>
              .
            </p>
            <p className="text-gray-700 mb-4">
              The VoiceLite name, logo, and branding are trademarks of VoiceLite. You may not use our
              trademarks without prior written permission.
            </p>
            <p className="text-gray-700">
              VoiceLite uses OpenAI's Whisper models, which are licensed under the MIT License.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">7. Disclaimer of Warranties</h2>
            <p className="text-gray-700 mb-4 uppercase font-semibold">
              The Software is provided "as is" without warranties of any kind, either express or implied.
            </p>
            <p className="text-gray-700 mb-4">
              We do not warrant that:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>The Software will be error-free or uninterrupted</li>
              <li>Transcriptions will be 100% accurate</li>
              <li>The Software will meet your specific requirements</li>
              <li>Defects will be corrected</li>
            </ul>
            <p className="text-gray-700">
              You use the Software at your own risk. We are not responsible for data loss, system damage, or
              any other harm resulting from use of the Software.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">8. Limitation of Liability</h2>
            <p className="text-gray-700 mb-4 uppercase font-semibold">
              To the maximum extent permitted by law, VoiceLite shall not be liable for any indirect,
              incidental, special, consequential, or punitive damages, or any loss of profits or revenues.
            </p>
            <p className="text-gray-700">
              Our total liability to you for any claim arising from these Terms or your use of the Software
              shall not exceed the amount you paid us in the 12 months prior to the claim, or $100 USD,
              whichever is less.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">9. Privacy</h2>
            <p className="text-gray-700">
              Your use of VoiceLite is also governed by our{' '}
              <a href="/privacy" className="text-blue-600 hover:underline">
                Privacy Policy
              </a>
              . By using the Software, you consent to our data practices as described in the Privacy Policy.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">10. Updates and Changes</h2>
            <p className="text-gray-700 mb-4">
              We may update the Software from time to time. Updates may:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>Add new features</li>
              <li>Fix bugs and improve performance</li>
              <li>Modify or remove existing features</li>
            </ul>
            <p className="text-gray-700">
              We are not obligated to provide updates, but when we do, they will be available for download
              from our website or GitHub.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">11. Changes to Terms</h2>
            <p className="text-gray-700">
              We may modify these Terms at any time. We will notify Pro users of material changes via email.
              Continued use of the Software after changes constitutes acceptance of the updated Terms. If you
              do not agree to the changes, you must stop using the Software and cancel your subscription.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">12. Governing Law</h2>
            <p className="text-gray-700">
              These Terms are governed by the laws of [Your State/Country], without regard to conflict of law
              principles. Any disputes shall be resolved in the courts of [Your Jurisdiction].
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">13. Severability</h2>
            <p className="text-gray-700">
              If any provision of these Terms is found to be unenforceable, the remaining provisions will
              remain in full force and effect.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">14. Contact Information</h2>
            <p className="text-gray-700 mb-4">
              For questions about these Terms or the Software:
            </p>
            <p className="text-gray-700">
              <strong>Email:</strong>{' '}
              <a href="mailto:contact@voicelite.app" className="text-blue-600 hover:underline">
                contact@voicelite.app
              </a>
              <br />
              <strong>Website:</strong>{' '}
              <a href="https://voicelite.app" className="text-blue-600 hover:underline">
                https://voicelite.app
              </a>
              <br />
              <strong>GitHub:</strong>{' '}
              <a
                href="https://github.com/mikha08-rgb/VoiceLite"
                className="text-blue-600 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                https://github.com/mikha08-rgb/VoiceLite
              </a>
            </p>
          </section>

          <div className="mt-12 pt-6 border-t border-gray-200">
            <p className="text-sm text-gray-500">
              By using VoiceLite, you acknowledge that you have read, understood, and agree to be bound by
              these Terms of Service.
            </p>
          </div>
        </div>

        <div className="mt-8 pt-6 border-t border-gray-200">
          <a href="/" className="text-blue-600 hover:underline">
            ← Back to Home
          </a>
        </div>
      </div>
    </div>
  );
}
