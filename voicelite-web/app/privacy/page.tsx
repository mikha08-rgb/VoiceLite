import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacy Policy - VoiceLite",
  description: "VoiceLite privacy policy: 100% offline voice processing, GDPR compliance, and data protection. Your voice never leaves your device.",
};

export default function PrivacyPolicy() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-6">
      <div className="max-w-4xl mx-auto bg-white rounded-lg shadow-md p-8">
        <h1 className="text-4xl font-bold text-gray-900 mb-6">Privacy Policy</h1>
        <p className="text-sm text-gray-500 mb-8">Last Updated: January 2025</p>

        <div className="prose prose-gray max-w-none">
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">1. Introduction</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite ("we," "our," or "us") is committed to protecting your privacy. This Privacy Policy
              explains how we collect, use, and safeguard your information when you use our VoiceLite
              desktop application and website (voicelite.app).
            </p>
            <p className="text-gray-700">
              <strong>Key Privacy Commitment:</strong> VoiceLite processes all voice data 100% offline on your
              local computer. Your voice recordings never leave your device and are automatically deleted
              after transcription.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">2. Information We Collect</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.1 Information Processed Locally (Never Sent to Us)</h3>
            <p className="text-gray-700 mb-4">
              The following data is processed entirely on your device and never transmitted to our servers:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>Voice recordings:</strong> Temporary audio files are created during transcription and immediately deleted after processing</li>
              <li><strong>Transcribed text:</strong> All text output remains on your device</li>
              <li><strong>Application settings:</strong> Hotkey preferences, model selection, and UI settings stored in %APPDATA%\VoiceLite\</li>
              <li><strong>Local error logs:</strong> The app automatically logs errors to %APPDATA%\VoiceLite\logs\ for troubleshooting. These logs are stored only on your device and are never transmitted to our servers. You can delete log files at any time.</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.2 Information We Collect (Pro Version Only)</h3>
            <p className="text-gray-700 mb-4">
              If you purchase VoiceLite Pro, we collect:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>Payment information:</strong> Processed securely by Stripe (we never see your full credit card details)</li>
              <li><strong>Email address:</strong> For subscription management, receipts, and support communications</li>
              <li><strong>License activation data:</strong> To prevent license sharing, we collect:
                <ul className="list-disc pl-6 mt-2 space-y-1">
                  <li>CPU Processor ID (hashed with SHA-256)</li>
                  <li>Windows Machine GUID from system registry (hashed with SHA-256)</li>
                  <li>These are combined and cryptographically hashed to create an anonymous device fingerprint</li>
                  <li>The original hardware IDs are never transmitted or stored - only the final hash</li>
                </ul>
              </li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.3 Website Analytics (Optional)</h3>
            <p className="text-gray-700 mb-4">
              Our website may use basic analytics to understand visitor traffic. We do not use tracking cookies
              that follow you across websites.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">3. How We Use Your Information</h2>
            <p className="text-gray-700 mb-4">We use collected information only for:</p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>Subscription Management:</strong> Processing payments, managing Pro licenses, sending receipts</li>
              <li><strong>Customer Support:</strong> Responding to inquiries and troubleshooting issues</li>
              <li><strong>Product Improvements:</strong> Anonymous error reports (if you opt-in) to fix bugs</li>
              <li><strong>Legal Compliance:</strong> Complying with applicable laws and regulations</li>
            </ul>
            <p className="text-gray-700">
              We will <strong>never</strong> sell, rent, or share your personal information with third parties for
              marketing purposes.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">4. Data Storage and Security</h2>
            <p className="text-gray-700 mb-4">
              <strong>Voice Data:</strong> Never stored. Temporary audio files are deleted immediately after
              transcription (typically within seconds).
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Payment Data:</strong> Handled exclusively by Stripe, a PCI-DSS Level 1 certified payment
              processor. We never store your payment card details.
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Account Data:</strong> Email addresses and subscription information are stored securely
              with industry-standard encryption.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">5. Third-Party Services</h2>
            <p className="text-gray-700 mb-4">We use the following third-party services:</p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li>
                <strong>Stripe:</strong> Payment processing and subscription management (
                <a href="https://stripe.com/privacy" className="text-blue-600 hover:underline" target="_blank" rel="noopener noreferrer">
                  Stripe Privacy Policy
                </a>)
              </li>
              <li>
                <strong>Resend:</strong> Transactional email delivery (receipts and subscription confirmations) (
                <a href="https://resend.com/legal/privacy-policy" className="text-blue-600 hover:underline" target="_blank" rel="noopener noreferrer">
                  Resend Privacy Policy
                </a>)
              </li>
            </ul>
            <p className="text-gray-700">
              These services have their own privacy policies. We only share the minimum information necessary
              to provide our services.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">6. Your Rights (GDPR Compliance)</h2>
            <p className="text-gray-700 mb-4">
              If you are in the European Economic Area (EEA) or other jurisdictions with data protection laws,
              you have the following rights:
            </p>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>Access:</strong> Request a copy of your personal data</li>
              <li><strong>Correction:</strong> Update or correct inaccurate information</li>
              <li><strong>Deletion:</strong> Request deletion of your account and associated data</li>
              <li><strong>Portability:</strong> Receive your data in a machine-readable format</li>
              <li><strong>Objection:</strong> Object to processing of your data for specific purposes</li>
            </ul>
            <p className="text-gray-700">
              To exercise these rights, email us at{' '}
              <a href="mailto:basementhustleLLC@gmail.com" className="text-blue-600 hover:underline">
                basementhustleLLC@gmail.com
              </a>
              . We will respond within 30 days.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">7. Data Retention</h2>
            <ul className="list-disc pl-6 mb-4 text-gray-700 space-y-2">
              <li><strong>Voice recordings:</strong> Deleted immediately after transcription (never stored)</li>
              <li><strong>Subscription data:</strong> Retained while your subscription is active, plus 7 years for tax/legal compliance</li>
              <li><strong>Account data:</strong> Deleted within 30 days of account closure upon request</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">8. Children's Privacy</h2>
            <p className="text-gray-700">
              VoiceLite is not intended for users under 13 years old. We do not knowingly collect personal
              information from children. If you believe we have collected data from a child, contact us
              immediately at{' '}
              <a href="mailto:basementhustleLLC@gmail.com" className="text-blue-600 hover:underline">
                basementhustleLLC@gmail.com
              </a>
              .
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">9. Changes to This Policy</h2>
            <p className="text-gray-700">
              We may update this Privacy Policy from time to time. We will notify Pro users of significant
              changes via email. Continued use of VoiceLite after changes constitutes acceptance of the
              updated policy.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">10. Contact Us</h2>
            <p className="text-gray-700 mb-4">
              If you have questions about this Privacy Policy or our data practices:
            </p>
            <p className="text-gray-700">
              <strong>Email:</strong>{' '}
              <a href="mailto:basementhustleLLC@gmail.com" className="text-blue-600 hover:underline">
                basementhustleLLC@gmail.com
              </a>
              <br />
              <strong>Website:</strong>{' '}
              <a href="https://voicelite.app" className="text-blue-600 hover:underline">
                https://voicelite.app
              </a>
            </p>
          </section>

          <div className="mt-12 pt-6 border-t border-gray-200">
            <p className="text-sm text-gray-500">
              By using VoiceLite, you acknowledge that you have read and understood this Privacy Policy.
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
