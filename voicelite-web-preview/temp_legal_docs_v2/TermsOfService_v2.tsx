import React from 'react';

export default function TermsOfService() {
  return (
    <div className="min-h-screen bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto bg-white shadow-sm rounded-lg p-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-4">Terms of Service</h1>
        <p className="text-sm text-gray-600 mb-8">Last Updated: January 2025</p>

        <div className="prose prose-blue max-w-none">
          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">1. Acceptance of Terms</h2>
            <p className="text-gray-700 mb-4">
              By accessing or using VoiceLite ("the Service"), you agree to be bound by these Terms of Service ("Terms"). If you do not agree to these Terms, do not use the Service.
            </p>
            <p className="text-gray-700 mb-4">
              These Terms apply to:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>The VoiceLite desktop application (Windows)</li>
              <li>The VoiceLite website (voicelite.app)</li>
              <li>All related services, features, and content</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">2. Description of Service</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite is a Windows desktop application that converts speech to text using OpenAI's Whisper AI. The Service includes:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Voice-to-text transcription:</strong> Real-time speech recognition</li>
              <li><strong>Global hotkey support:</strong> Push-to-talk or toggle recording modes</li>
              <li><strong>Text injection:</strong> Automatic insertion into any Windows application</li>
              <li><strong>Offline operation:</strong> All processing happens locally on your device</li>
              <li><strong>Multiple AI models:</strong> Choose between speed and accuracy</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">3. Subscription Plans</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.1 Free Tier (Limited-Time Promotion)</h3>
            <p className="text-gray-700 mb-4">
              The Free Tier includes:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Pro AI model (466MB, ~90-93% accuracy)</strong> included free as limited-time promotion. Lite model (75MB) available as fallback.</li>
              <li>Unlimited transcriptions (no usage caps)</li>
              <li>All core features (hotkeys, text injection, custom dictionaries)</li>
              <li>100% offline operation (no internet required)</li>
              <li>No license validation or account creation</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>Note:</strong> The Pro model promotion is temporary and may change in future updates. Existing installations will continue to work with the Pro model, but new installations may default to a different free tier model.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.2 Pro Tier</h3>
            <p className="text-gray-700 mb-4">
              The Pro Tier unlocks:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Premium AI models:</strong> Swift (142MB), Elite (1.5GB), Ultra (2.9GB)</li>
              <li><strong>Higher accuracy:</strong> 93-97% transcription accuracy</li>
              <li><strong>Priority support:</strong> Faster response times</li>
              <li><strong>Early access:</strong> Beta features and new models</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>Pricing:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li><strong>Quarterly Subscription:</strong> $20 USD per 3 months (auto-renewing)</li>
              <li><strong>Lifetime License:</strong> $99 USD one-time payment (permanent access, no renewals)</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>Device Limit:</strong> Pro licenses allow activation on up to 3 devices simultaneously. You can deactivate devices via the customer portal to free up slots.
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Lifetime License Details:</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>One-time payment of $99 USD (no recurring charges)</li>
              <li>Permanent access to Pro tier features (no expiration)</li>
              <li>Includes all future updates and new Pro models</li>
              <li>3 simultaneous device activations (same as quarterly subscription)</li>
              <li>30-day money-back guarantee</li>
              <li>License is non-transferable and tied to your account</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">3.3 Price Changes</h3>
            <p className="text-gray-700 mb-4">
              We reserve the right to change pricing with 30 days notice. Existing subscribers will maintain their original pricing for the duration of their current subscription term. Lifetime licenses are not affected by price changes.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">4. Payment and Billing</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.1 Payment Processing</h3>
            <p className="text-gray-700 mb-4">
              Payments are processed securely via Stripe. We do not store your credit card information. By providing payment information, you authorize us to charge the applicable fees.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.2 Refunds</h3>
            <p className="text-gray-700 mb-4">
              <strong>Quarterly Subscriptions ($20/3mo):</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>14-day money-back guarantee from initial purchase date</li>
              <li>Refunds processed within 7 business days</li>
              <li>Cancellations do not provide refunds; service continues until end of billing period</li>
            </ul>
            <p className="text-gray-700 mb-4">
              <strong>Lifetime Licenses ($99 one-time):</strong>
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>30-day money-back guarantee from purchase date</li>
              <li>No refunds after 30 days or if license has been activated on 3+ devices</li>
              <li>Refunds processed via original payment method within 7 business days</li>
            </ul>
            <p className="text-gray-700 mb-4">
              To request a refund, contact: contact@voicelite.app with your license key and reason.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.3 Cancellation</h3>
            <p className="text-gray-700 mb-4">
              You may cancel your quarterly subscription at any time via the customer portal. Your access will continue until the end of the current billing period. No partial refunds are issued for cancellations.
            </p>
            <p className="text-gray-700 mb-4">
              Lifetime licenses cannot be canceled (permanent access), but you may request a refund within 30 days.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">4.4 Failed Payments</h3>
            <p className="text-gray-700 mb-4">
              If a recurring payment fails, we will attempt to charge your payment method up to 3 times over 7 days. If all attempts fail, your subscription will be suspended. You will have a 7-day grace period to update payment information before access is revoked.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">5. License and Restrictions</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">5.1 License Grant</h3>
            <p className="text-gray-700 mb-4">
              Subject to these Terms, we grant you a limited, non-exclusive, non-transferable, revocable license to:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Install and use VoiceLite on devices you own or control</li>
              <li>Use the Service for personal or commercial purposes</li>
              <li>Modify the source code under the terms of the MIT License (see EULA)</li>
            </ul>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">5.2 Restrictions</h3>
            <p className="text-gray-700 mb-4">
              You may NOT:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Redistribute or sell the Service (binaries or modified versions)</li>
              <li>Reverse engineer the Service to circumvent license validation</li>
              <li>Share your Pro license key with others</li>
              <li>Use the Service for illegal activities</li>
              <li>Abuse the Service (e.g., excessive API requests, DoS attacks)</li>
              <li>Remove copyright notices or branding</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">6. User Responsibilities</h2>
            <p className="text-gray-700 mb-4">
              You are responsible for:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Maintaining the confidentiality of your account credentials</li>
              <li>All activities under your account</li>
              <li>Ensuring your use complies with applicable laws</li>
              <li>Securing your devices against unauthorized access</li>
              <li>Reporting security vulnerabilities to contact@voicelite.app</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">7. Privacy</h2>
            <p className="text-gray-700 mb-4">
              Your use of the Service is governed by our <a href="/privacy" className="text-blue-600 hover:underline">Privacy Policy</a>. Key highlights:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Voice recordings are processed locally and NEVER uploaded</li>
              <li>Analytics are opt-in only and use anonymous IDs</li>
              <li>We collect minimal data (email, payment info for Pro tier)</li>
              <li>We do not sell your data to third parties</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">8. Acceptable Use</h2>
            <p className="text-gray-700 mb-4">
              You agree NOT to use VoiceLite for:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Illegal activities (fraud, hacking, etc.)</li>
              <li>Harassment, hate speech, or threats</li>
              <li>Spamming or phishing</li>
              <li>Violating intellectual property rights</li>
              <li>Distributing malware or viruses</li>
              <li>Circumventing security measures</li>
            </ul>
            <p className="text-gray-700 mb-4">
              Violations may result in immediate termination without refund.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">9. Intellectual Property</h2>
            <p className="text-gray-700 mb-4">
              VoiceLite and all related content (code, design, branding) are owned by VoiceLite or its licensors. The source code is licensed under the MIT License (see repository), but binaries and Pro features are governed by the EULA.
            </p>
            <p className="text-gray-700 mb-4">
              Third-party components (Whisper AI, NAudio, etc.) are subject to their respective licenses.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">10. Disclaimers</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">10.1 Service Availability</h3>
            <p className="text-gray-700 mb-4">
              We strive for 99.9% uptime, but the Service is provided "AS IS" without guarantees. We may experience downtime for maintenance, updates, or unforeseen issues. We are not liable for service interruptions.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">10.2 Transcription Accuracy</h3>
            <p className="text-gray-700 mb-4">
              VoiceLite uses AI-powered speech recognition, which is not 100% accurate. Accuracy varies based on:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Audio quality and microphone</li>
              <li>Speaker accent and clarity</li>
              <li>Background noise</li>
              <li>AI model used (Lite vs Pro vs Elite)</li>
              <li>Language and technical terminology</li>
            </ul>
            <p className="text-gray-700 mb-4">
              We do not guarantee specific accuracy rates. Always review transcriptions before use in critical contexts.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">10.3 No Warranty</h3>
            <p className="text-gray-700 mb-4">
              THE SERVICE IS PROVIDED "AS IS" WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Merchantability</li>
              <li>Fitness for a particular purpose</li>
              <li>Non-infringement</li>
              <li>Uninterrupted or error-free operation</li>
            </ul>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">11. Limitation of Liability</h2>
            <p className="text-gray-700 mb-4">
              TO THE MAXIMUM EXTENT PERMITTED BY LAW, VOICELITE SHALL NOT BE LIABLE FOR:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Indirect, incidental, special, or consequential damages</li>
              <li>Loss of profits, data, or business opportunities</li>
              <li>Damages arising from use or inability to use the Service</li>
              <li>Damages exceeding the amount you paid in the last 12 months</li>
            </ul>
            <p className="text-gray-700 mb-4">
              Some jurisdictions do not allow limitation of liability, so this may not apply to you.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">12. Governing Law</h2>
            <p className="text-gray-700 mb-4">
              These Terms are governed by the laws of the State of Illinois, United States of America, without regard to conflict of law principles. Any disputes shall be resolved in the courts of Illinois.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">13. Dispute Resolution</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">13.1 Informal Resolution</h3>
            <p className="text-gray-700 mb-4">
              Before filing a legal claim, you agree to contact us at contact@voicelite.app to resolve disputes informally. We will respond within 7 business days.
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">13.2 Arbitration</h3>
            <p className="text-gray-700 mb-4">
              If informal resolution fails, disputes will be resolved via binding arbitration under the rules of the American Arbitration Association (AAA). You waive the right to a jury trial and class action lawsuits.
            </p>
            <p className="text-gray-700 mb-4">
              Exceptions: You may bring claims in small claims court if they qualify.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">14. Termination</h2>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">14.1 By You</h3>
            <p className="text-gray-700 mb-4">
              You may terminate your use of the Service at any time by uninstalling the app and canceling your subscription (if applicable).
            </p>

            <h3 className="text-xl font-semibold text-gray-800 mb-3">14.2 By VoiceLite</h3>
            <p className="text-gray-700 mb-4">
              We may terminate your access if you:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Violate these Terms</li>
              <li>Engage in fraudulent activity</li>
              <li>Abuse the Service or backend infrastructure</li>
              <li>Chargeback a legitimate payment</li>
            </ul>
            <p className="text-gray-700 mb-4">
              Upon termination, your license is revoked, and you must uninstall the Software. No refunds will be issued for violations.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">15. Changes to Terms</h2>
            <p className="text-gray-700 mb-4">
              We may update these Terms from time to time. Changes will be posted at voicelite.app/terms with an updated "Last Updated" date. For material changes, we will notify you via:
            </p>
            <ul className="list-disc pl-6 text-gray-700 mb-4">
              <li>Email (if you have an account)</li>
              <li>In-app notification</li>
              <li>Prominent notice on our website</li>
            </ul>
            <p className="text-gray-700 mb-4">
              Continued use of the Service after changes constitutes acceptance of the updated Terms.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">16. Severability</h2>
            <p className="text-gray-700 mb-4">
              If any provision of these Terms is found invalid or unenforceable, the remaining provisions will remain in full force and effect.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">17. Entire Agreement</h2>
            <p className="text-gray-700 mb-4">
              These Terms, along with the EULA and Privacy Policy, constitute the entire agreement between you and VoiceLite regarding the Service.
            </p>
          </section>

          <section className="mb-8">
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">18. Contact</h2>
            <p className="text-gray-700 mb-4">
              For questions about these Terms, contact:
            </p>
            <p className="text-gray-700 mb-4">
              <strong>Email:</strong> contact@voicelite.app<br />
              <strong>Website:</strong> <a href="https://voicelite.app" className="text-blue-600 hover:underline">https://voicelite.app</a><br />
              <strong>GitHub:</strong> <a href="https://github.com/mikha08-rgb/VoiceLite" className="text-blue-600 hover:underline">https://github.com/mikha08-rgb/VoiceLite</a>
            </p>
          </section>

          <section>
            <h2 className="text-2xl font-semibold text-gray-900 mb-4">19. Summary</h2>
            <div className="bg-blue-50 border-l-4 border-blue-500 p-4">
              <p className="text-gray-700 mb-2">
                <strong>TL;DR (Too Long; Didn't Read):</strong>
              </p>
              <ul className="list-disc pl-6 text-gray-700">
                <li>Free Tier includes Pro model (466MB, ~90-93% accuracy) as limited-time promotion</li>
                <li>Pro Tier costs $20/3mo or $99 lifetime for premium models (93-97% accuracy)</li>
                <li>14-day refund for quarterly subscriptions, 30-day refund for lifetime licenses</li>
                <li>Up to 3 device activations per Pro license</li>
                <li>Don't abuse the Service or violate laws</li>
                <li>We're not liable for transcription errors or service downtime</li>
                <li>Disputes go to arbitration in Illinois courts</li>
              </ul>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
