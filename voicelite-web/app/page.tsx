'use client';

import { useState } from 'react';
import { Mic, Shield, Zap, Check, X, Download, CreditCard } from 'lucide-react';

export default function Home() {
  const [isLoading, setIsLoading] = useState(false);

  const handleCheckout = async () => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/checkout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });
      const data = await response.json();
      if (data.url) {
        window.location.href = data.url;
      }
    } catch (error) {
      console.error('Checkout error:', error);
      alert('Something went wrong. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <main className="min-h-screen bg-gradient-to-b from-gray-50 to-white">
      {/* Hero Section */}
      <section className="px-6 py-20 text-center">
        <div className="max-w-4xl mx-auto">
          <div className="inline-flex items-center px-3 py-1 mb-6 text-sm font-medium text-blue-700 bg-blue-100 rounded-full">
            <Mic className="w-4 h-4 mr-2" />
            100% Offline Voice Typing for Windows
          </div>

          <h1 className="text-5xl md:text-6xl font-bold text-gray-900 mb-6">
            Turn Your Voice Into Text
            <br />
            <span className="text-blue-600">Instantly</span>
          </h1>

          <p className="text-xl text-gray-600 mb-8 max-w-2xl mx-auto">
            Hold Alt, speak naturally, release. Your words appear as typed text in ANY Windows application.
            No internet required. Your voice never leaves your PC.
          </p>

          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <a
              href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.3/VoiceLite-Setup-1.0.3.exe"
              download
              className="inline-flex items-center px-8 py-4 text-lg font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              <Download className="w-5 h-5 mr-2" />
              Download VoiceLite Free
            </a>
          </div>

          <p className="mt-4 text-sm text-gray-500">
            Windows 10/11 • No credit card for free version
          </p>
        </div>
      </section>

      {/* Features Grid */}
      <section className="px-6 py-20">
        <div className="max-w-6xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-12">
            Why VoiceLite?
          </h2>

          <div className="grid md:grid-cols-3 gap-8">
            <div className="text-center">
              <div className="inline-flex items-center justify-center w-16 h-16 mb-4 text-blue-600 bg-blue-100 rounded-full">
                <Shield className="w-8 h-8" />
              </div>
              <h3 className="text-xl font-semibold mb-2">100% Private</h3>
              <p className="text-gray-600">
                Completely offline. Your voice never leaves your computer. No cloud, no tracking.
              </p>
            </div>

            <div className="text-center">
              <div className="inline-flex items-center justify-center w-16 h-16 mb-4 text-green-600 bg-green-100 rounded-full">
                <Zap className="w-8 h-8" />
              </div>
              <h3 className="text-xl font-semibold mb-2">Lightning Fast</h3>
              <p className="text-gray-600">
                Instant transcription. Less than 200ms from speech to text. No waiting.
              </p>
            </div>

            <div className="text-center">
              <div className="inline-flex items-center justify-center w-16 h-16 mb-4 text-purple-600 bg-purple-100 rounded-full">
                <Mic className="w-8 h-8" />
              </div>
              <h3 className="text-xl font-semibold mb-2">Works Everywhere</h3>
              <p className="text-gray-600">
                Discord, VS Code, Word, Terminal, Games. Any Windows app, any text field.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Comparison Table */}
      <section className="px-6 py-20 bg-gray-50">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-4">
            Choose Your Version
          </h2>
          <p className="text-center text-gray-600 mb-12">
            Start free, upgrade when you need more power
          </p>

          <div className="bg-white rounded-xl shadow-lg overflow-hidden">
            <div className="grid md:grid-cols-3 divide-x divide-gray-200">
              <div className="p-6">
                <h3 className="text-lg font-semibold text-gray-500">Features</h3>
              </div>
              <div className="p-6 text-center bg-gray-50">
                <h3 className="text-lg font-semibold">Free</h3>
                <p className="text-3xl font-bold mt-2">$0</p>
                <p className="text-sm text-gray-500">Forever</p>
              </div>
              <div className="p-6 text-center bg-blue-50 relative">
                <div className="absolute -top-4 left-1/2 transform -translate-x-1/2">
                  <span className="bg-blue-600 text-white text-xs px-3 py-1 rounded-full">POPULAR</span>
                </div>
                <h3 className="text-lg font-semibold text-blue-900">Pro</h3>
                <p className="text-3xl font-bold mt-2 text-blue-900">$7</p>
                <p className="text-sm text-blue-700">per month</p>
              </div>
            </div>

            <div className="divide-y divide-gray-200">
              {[
                { feature: 'Tiny AI Model (Fastest)', free: true, pro: true },
                { feature: 'All 5 AI Models (Better Accuracy)', free: false, pro: true },
                { feature: '100% Offline', free: true, pro: true },
                { feature: 'Custom Hotkeys', free: true, pro: true },
                { feature: 'Smart Text Injection', free: true, pro: true },
                { feature: 'Unlimited Usage', free: true, pro: true },
                { feature: 'Priority Support', free: false, pro: true },
              ].map((item, index) => (
                <div key={index} className="grid md:grid-cols-3 divide-x divide-gray-200">
                  <div className="p-4">
                    <span className="text-gray-700">{item.feature}</span>
                  </div>
                  <div className="p-4 text-center bg-gray-50">
                    {item.free ? (
                      <Check className="w-5 h-5 text-green-500 mx-auto" />
                    ) : (
                      <X className="w-5 h-5 text-gray-300 mx-auto" />
                    )}
                  </div>
                  <div className="p-4 text-center bg-blue-50">
                    <Check className="w-5 h-5 text-blue-600 mx-auto" />
                  </div>
                </div>
              ))}
            </div>

            <div className="grid md:grid-cols-3 divide-x divide-gray-200">
              <div className="p-6"></div>
              <div className="p-6 text-center bg-gray-50">
                <a
                  href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.3/VoiceLite-Setup-1.0.3.exe"
                  download
                  className="inline-flex items-center px-6 py-3 text-sm font-semibold text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  Download Free
                </a>
              </div>
              <div className="p-6 text-center bg-blue-50">
                <div className="inline-flex items-center px-6 py-3 text-sm font-semibold text-blue-900 bg-blue-100 rounded-lg">
                  Coming Soon
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* How It Works */}
      <section className="px-6 py-20">
        <div className="max-w-4xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-12">
            How It Works
          </h2>

          <div className="grid md:grid-cols-3 gap-8">
            <div className="text-center">
              <div className="text-4xl font-bold text-blue-600 mb-4">1</div>
              <h3 className="text-xl font-semibold mb-2">Download & Install</h3>
              <p className="text-gray-600">
                One-click install. No complex setup. Works in seconds.
              </p>
            </div>

            <div className="text-center">
              <div className="text-4xl font-bold text-blue-600 mb-4">2</div>
              <h3 className="text-xl font-semibold mb-2">Hold Alt Key</h3>
              <p className="text-gray-600">
                Press and hold the Alt key (or customize your hotkey in Pro).
              </p>
            </div>

            <div className="text-center">
              <div className="text-4xl font-bold text-blue-600 mb-4">3</div>
              <h3 className="text-xl font-semibold mb-2">Speak & Release</h3>
              <p className="text-gray-600">
                Speak naturally, release the key. Text appears instantly.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* FAQ */}
      <section className="px-6 py-20 bg-gray-50">
        <div className="max-w-3xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-12">
            Frequently Asked Questions
          </h2>

          <div className="space-y-6">
            <div className="bg-white p-6 rounded-lg">
              <h3 className="font-semibold mb-2">Does it need internet?</h3>
              <p className="text-gray-600">
                No! VoiceLite works 100% offline. Your voice never leaves your computer.
                Internet is only needed once to activate the Pro version.
              </p>
            </div>

            <div className="bg-white p-6 rounded-lg">
              <h3 className="font-semibold mb-2">How accurate is it?</h3>
              <p className="text-gray-600">
                Very accurate! 95%+ on normal speech, excellent with technical terms like git, npm, useState.
                Pro version includes larger models for even better accuracy.
              </p>
            </div>

            <div className="bg-white p-6 rounded-lg">
              <h3 className="font-semibold mb-2">What&apos;s the difference between Free and Pro?</h3>
              <p className="text-gray-600">
                Free version includes the fast tiny model - perfect for basic use but may struggle with technical terms.
                Pro unlocks all 5 AI models (tiny, base, small, medium, large) for much better accuracy,
                especially with programming terms, medical terminology, and accents.
              </p>
            </div>

            <div className="bg-white p-6 rounded-lg">
              <h3 className="font-semibold mb-2">Can I cancel anytime?</h3>
              <p className="text-gray-600">
                Yes! Cancel anytime from your Stripe customer portal. You&apos;ll keep Pro features until
                the end of your billing period. The app continues working offline even after cancellation.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="px-6 py-12 bg-gray-900 text-white">
        <div className="max-w-6xl mx-auto text-center">
          <h3 className="text-2xl font-bold mb-4">VoiceLite</h3>
          <p className="text-gray-400 mb-8">
            The fastest way to type with your voice on Windows
          </p>

          <div className="flex justify-center space-x-6 mb-8">
            <a href="/privacy" className="text-gray-400 hover:text-white">Privacy Policy</a>
            <a href="/terms" className="text-gray-400 hover:text-white">Terms</a>
            <a href="mailto:support@voicelite.app" className="text-gray-400 hover:text-white">Support</a>
          </div>

          <p className="text-gray-500 text-sm">
            © 2024 VoiceLite. Built with ❤️ for the Windows community.
          </p>
        </div>
      </footer>
    </main>
  );
}