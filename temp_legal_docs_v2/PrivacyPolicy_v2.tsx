import React from 'react';

export default function PrivacyPolicy() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto bg-white shadow-sm rounded-lg p-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-4">Privacy Policy</h1>
        <p className="text-sm text-gray-600 mb-8">Last Updated: January 2025</p>

        <div className="prose prose-blue max-w-none">
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">1. Introduction</h2>
            <p className="text-gray-700 mb-4">
              Welcome to VoiceLite. We are committed to protecting your privacy and ensuring transparency about our data practices. This Privacy Policy explains what data we collect, how we use it, and your rights regarding your personal information.
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Key Privacy Principles:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Your voice recordings are processed locally and NEVER uploaded to our servers</li>
              <li>Analytics are opt-in only and use anonymous IDs (SHA-256 hashed)</li>
              <li>We collect minimal data required for service delivery</li>
              <li>You have full control over your data</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">2. Data We Collect</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.1 Voice Recordings (Desktop App)</h3>
            <p className="text-gray-700 mb-4">
              <strong>What we collect:</strong> NOTHING. Your voice recordings are processed entirely on your local device.
            </p>
            <p className="text-gray-700 mb-4">
              <strong>How it works:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>You record audio via microphone using global hotkey</li>
              <li>Audio is saved temporarily to your device (e.g., %TEMP%\recording.wav)</li>
              <li>OpenAI Whisper AI processes the audio locally (runs on your CPU)</li>
              <li>Transcribed text is injected into your active application</li>
              <li>Audio file is immediately deleted after transcription</li>
              <li>No audio data is ever transmitted to VoiceLite servers or third parties</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.2 Account Data (Pro Tier)</h3>
            <p className="text-gray-700 mb-4">
              When you purchase a Pro subscription, we collect:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Email address:</strong> For account management, license delivery, and support</li>
              <li><strong>Payment information:</strong> Processed securely via Stripe (we do NOT store credit card numbers)</li>
              <li><strong>License activation data:</strong> Device fingerprints (hashed CPU ID + Machine GUID), activation timestamps, device count</li>
              <li><strong>Subscription status:</strong> Plan type (quarterly/lifetime), renewal dates, cancellation status</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.3 Desktop Application Analytics (Opt-In)</h3>
            <p className="text-gray-700 mb-4">
              VoiceLite includes <strong>optional, privacy-first analytics</strong> that help us improve the app. Analytics are <strong>DISABLED by default</strong> and require your explicit consent.
            </p>
            <p className="text-gray-700 mb-4">
              <strong>How it works:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>On first launch, you'll see a consent dialog explaining analytics</li>
              <li>You can opt-in, opt-out, or decide later</li>
              <li>You can change your preference anytime in Settings</li>
              <li>If analytics fail, the app continues working normally (silent failures)</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>What we collect (if you opt in):</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Anonymous User ID:</strong> SHA-256 hash of (Machine ID + Timestamp) - irreversible, no PII</li>
              <li><strong>APP_LAUNCHED events:</strong> App starts (tier, version, OS version)</li>
              <li><strong>TRANSCRIPTION_COMPLETED events:</strong> Aggregated daily (count, total words, model used) - NO transcription content</li>
              <li><strong>MODEL_CHANGED events:</strong> Model switches (old model → new model)</li>
              <li><strong>SETTINGS_CHANGED events:</strong> Setting name only (no values)</li>
              <li><strong>ERROR_OCCURRED events:</strong> Error types for debugging (no stack traces, no file paths)</li>
              <li><strong>PRO_UPGRADE events:</strong> Pro tier activation timestamps</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>What we DO NOT collect:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Recording content or transcription text</li>
              <li>File paths or directory structures</li>
              <li>IP addresses (backend does not log IPs)</li>
              <li>Personally identifiable information (PII)</li>
              <li>User names, email addresses, or device names</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.4 Website Usage Data</h3>
            <p className="text-gray-700 mb-4">
              When you visit voicelite.app, we collect:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Server logs:</strong> IP address (anonymized after 7 days), browser type, pages visited, timestamps</li>
              <li><strong>Cookies:</strong> Session cookies for login state, analytics cookies (if you consent)</li>
              <li><strong>Analytics:</strong> We use privacy-respecting analytics (no third-party trackers like Google Analytics)</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">2.5 Support Communications</h3>
            <p className="text-gray-700 mb-4">
              When you contact support, we collect:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Email correspondence and attachments you send</li>
              <li>Error logs you voluntarily submit</li>
              <li>System information you provide (OS version, app version, etc.)</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">3. How We Use Your Data</h2>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Account management:</strong> Process subscriptions, deliver licenses, manage renewals</li>
              <li><strong>Customer support:</strong> Respond to inquiries, troubleshoot issues, provide assistance</li>
              <li><strong>Product improvement:</strong> Analyze aggregated analytics to improve features and fix bugs (opt-in only)</li>
              <li><strong>Security:</strong> Detect fraud, prevent abuse, enforce license terms</li>
              <li><strong>Legal compliance:</strong> Comply with tax laws, respond to legal requests</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>We do NOT:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Sell your data to third parties</li>
              <li>Use your data for advertising</li>
              <li>Share your data except as described in this policy</li>
              <li>Train AI models on your voice recordings (we never see them)</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">4. Data Sharing</h2>
            <p className="text-gray-700 mb-4">
              We share data only when necessary:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Stripe:</strong> Payment processing (PCI DSS compliant)</li>
              <li><strong>Resend:</strong> Transactional emails (license delivery, password resets)</li>
              <li><strong>Vercel:</strong> Hosting infrastructure (SOC 2 Type II certified)</li>
              <li><strong>Supabase:</strong> Database hosting (PostgreSQL, encrypted at rest)</li>
              <li><strong>Legal requests:</strong> When required by law (e.g., subpoenas, court orders)</li>
            </ul>
            <p className="text-gray-700 mb-4">
              We require all third-party vendors to maintain strict confidentiality and security standards.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">5. Data Security</h2>
            <p className="text-gray-700 mb-4">
              We implement industry-standard security measures:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Encryption in transit:</strong> HTTPS/TLS 1.3 for all connections</li>
              <li><strong>Encryption at rest:</strong> Database encryption via Supabase</li>
              <li><strong>License signing:</strong> Ed25519 cryptographic signatures (tamper-proof)</li>
              <li><strong>Password hashing:</strong> Bcrypt with per-user salts</li>
              <li><strong>Rate limiting:</strong> Upstash Redis prevents abuse (5 requests/hour on sensitive endpoints)</li>
              <li><strong>Access controls:</strong> Role-based permissions, audit logging</li>
            </ul>
            <p className="text-gray-700 mb-4">
              However, no system is 100% secure. If you discover a security vulnerability, please report it to: contact@voicelite.app
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">6. Data Retention</h2>
            <p className="text-gray-700 mb-4">
              We retain data for different periods:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Voice recordings:</strong> Deleted immediately after transcription (never stored)</li>
              <li><strong>Transcription history:</strong> Stored locally on your device (you control retention in settings)</li>
              <li><strong>Account data:</strong> Retained for 7 years after account closure (tax/legal compliance)</li>
              <li><strong>Analytics data:</strong> Retained for 2 years, then automatically deleted (if opted in)</li>
              <li><strong>Support emails:</strong> Retained for 3 years</li>
              <li><strong>Server logs:</strong> IP addresses anonymized after 7 days, logs deleted after 90 days</li>
            </ul>
            <p className="text-gray-700 mb-4">
              You may request earlier deletion by contacting: contact@voicelite.app
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">7. Your Rights</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">7.1 GDPR Rights (EU/EEA Residents)</h3>
            <p className="text-gray-700 mb-4">
              If you are in the EU/EEA, you have the right to:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Access:</strong> Request a copy of your personal data</li>
              <li><strong>Rectification:</strong> Correct inaccurate data</li>
              <li><strong>Erasure:</strong> Request deletion of your data ("right to be forgotten")</li>
              <li><strong>Portability:</strong> Receive your data in machine-readable format</li>
              <li><strong>Restriction:</strong> Limit how we process your data</li>
              <li><strong>Objection:</strong> Object to processing based on legitimate interests</li>
              <li><strong>Withdraw consent:</strong> Opt-out of analytics at any time</li>
            </ul>
            <p className="text-gray-700 mb-4">
              To exercise these rights, contact: contact@voicelite.app
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">7.2 CCPA Rights (California Residents)</h3>
            <p className="text-gray-700 mb-4">
              If you are a California resident, you have the right to:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Access:</strong> Request disclosure of personal information collected in the past 12 months</li>
              <li><strong>Deletion:</strong> Request deletion of your personal information (subject to legal exceptions)</li>
              <li><strong>Opt-out of sale:</strong> We do NOT sell your data, so this right is not applicable</li>
              <li><strong>Non-discrimination:</strong> We will not discriminate against you for exercising your CCPA rights</li>
            </ul>
            <p className="text-gray-700 mb-4">
              To submit a CCPA request, contact: contact@voicelite.app with subject line "CCPA Request"
            </p>
            <p className="text-gray-700 mb-4">
              We will verify your identity before processing requests and respond within 45 days.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">7.3 All Users</h3>
            <p className="text-gray-700 mb-4">
              Regardless of location, you can:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Disable analytics in Settings</li>
              <li>Uninstall the app at any time</li>
              <li>Cancel your subscription via customer portal</li>
              <li>Request data deletion by contacting support</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">8. Children's Privacy</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite is not intended for users under 13 years of age. We do not knowingly collect data from children. If you believe a child has provided us with personal information, contact: contact@voicelite.app
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">9. International Data Transfers</h2>
            <p className="text-gray-700 mb-4">
              Your data may be transferred to and stored in the United States. By using VoiceLite, you consent to this transfer. We ensure adequate protections through:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Standard Contractual Clauses (SCCs) with EU vendors</li>
              <li>Encryption in transit and at rest</li>
              <li>Compliance with GDPR and CCPA standards</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">10. Changes to This Policy</h2>
            <p className="text-gray-700 mb-4">
              We may update this Privacy Policy from time to time. Changes will be posted at voicelite.app/privacy with an updated "Last Updated" date. For material changes, we will notify you via:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Email (if you have an account)</li>
              <li>In-app notification</li>
              <li>Prominent notice on our website</li>
            </ul>
            <p className="text-gray-700 mb-4">
              Continued use of VoiceLite after changes constitutes acceptance of the updated policy.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">11. Contact Us</h2>
            <p className="text-gray-700 mb-4">
              For privacy questions, data requests, or concerns, contact:
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Email:</strong> contact@voicelite.app<br />
              <strong>Website:</strong> <a href="https://voicelite.app" className="text-blue-600 hover:underline">https://voicelite.app</a><br />
              <strong>GitHub:</strong> <a href="https://github.com/mikha08-rgb/VoiceLite" className="text-blue-600 hover:underline">https://github.com/mikha08-rgb/VoiceLite</a>
            </p>
            <p className="text-gray-700 mb-4">
              We will respond to privacy inquiries within 7 business days.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">12. Third-Party Services</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite integrates with the following third-party services:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Stripe:</strong> Payment processing - <a href="https://stripe.com/privacy" className="text-blue-600 hover:underline">Privacy Policy</a></li>
              <li><strong>Resend:</strong> Transactional emails - <a href="https://resend.com/privacy" className="text-blue-600 hover:underline">Privacy Policy</a></li>
              <li><strong>Vercel:</strong> Web hosting - <a href="https://vercel.com/legal/privacy-policy" className="text-blue-600 hover:underline">Privacy Policy</a></li>
              <li><strong>Supabase:</strong> Database hosting - <a href="https://supabase.com/privacy" className="text-blue-600 hover:underline">Privacy Policy</a></li>
              <li><strong>Upstash Redis:</strong> Rate limiting - <a href="https://upstash.com/privacy" className="text-blue-600 hover:underline">Privacy Policy</a></li>
            </ul>
            <p className="text-gray-700 mb-4">
              We are not responsible for third-party privacy practices. Please review their policies independently.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">13. Data Breach Notification</h2>
            <p className="text-gray-700 mb-4">
              In the event of a data breach affecting your personal information, we will:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Notify affected users within 72 hours via email</li>
              <li>Disclose the nature of the breach, data affected, and remediation steps</li>
              <li>Report to relevant authorities as required by law (GDPR, CCPA)</li>
            </ul>
          </section>

          <section>
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">14. Summary</h2>
            <div className="bg-blue-50 border-l-4 border-blue-500 p-4">
              <p className="text-gray-700 mb-2">
                <strong>TL;DR (Too Long; Didn't Read):</strong>
              </p>
              <ul className="list-disc pl-6 text-gray-700">
                <li>Your voice recordings stay on your device - we NEVER upload them</li>
                <li>Analytics are opt-in only and use anonymous IDs (SHA-256 hashed)</li>
                <li>We collect minimal data (email, payment info for Pro tier)</li>
                <li>We don't sell your data or use it for ads</li>
                <li>You can request deletion anytime</li>
                <li>We use industry-standard security (encryption, rate limiting)</li>
              </ul>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
