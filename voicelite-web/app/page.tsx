'use client';

import { useState } from 'react';
import { Download, Menu, X } from 'lucide-react';
import Link from 'next/link';

export default function HomePage() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [openFaqIndex, setOpenFaqIndex] = useState<number | null>(null);
  const [isCheckoutLoading, setIsCheckoutLoading] = useState(false);

  const handleGetProClick = async () => {
    setIsCheckoutLoading(true);
    try {
      const response = await fetch('/api/checkout', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to create checkout session');
      }

      const data = await response.json();
      if (data.url) {
        window.location.href = data.url;
      }
    } catch (error) {
      console.error('Checkout error:', error);
      alert('Failed to start checkout. Please try again.');
      setIsCheckoutLoading(false);
    }
  };

  return (
    <main className="min-h-screen bg-white dark:bg-stone-950">
      {/* Navigation */}
      <nav className="sticky top-0 z-50 border-b border-stone-200 bg-white/95 backdrop-blur-sm dark:border-stone-800 dark:bg-stone-950/95">
        <div className="container mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
          <Link href="/" className="flex items-center gap-2 text-xl font-bold text-blue-600 dark:text-blue-400">
            🎤 <span>VoiceLite</span>
          </Link>

          {/* Desktop Navigation */}
          <div className="hidden items-center gap-8 md:flex">
            <a href="#features" className="text-sm font-medium text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
              Features
            </a>
            <a href="#pricing" className="text-sm font-medium text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
              Pricing
            </a>
            <a href="#faq" className="text-sm font-medium text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
              FAQ
            </a>
            <a
              href="https://github.com/mikha08-rgb/VoiceLite"
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm font-medium text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400"
            >
              GitHub
            </a>
            <a
              href="#pricing"
              className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-md shadow-blue-600/30 transition-all hover:bg-blue-700 hover:shadow-lg hover:shadow-blue-600/40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
            >
              Get Pro
            </a>
          </div>

          {/* Mobile Menu Button */}
          <button
            onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
            className="rounded-lg p-2 text-stone-600 transition-colors hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800 md:hidden"
            aria-label="Toggle menu"
          >
            {isMobileMenuOpen ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
          </button>
        </div>

        {/* Mobile Menu */}
        {isMobileMenuOpen && (
          <div className="border-t border-stone-200 bg-white dark:border-stone-800 dark:bg-stone-950 md:hidden">
            <div className="container mx-auto max-w-7xl space-y-1 px-6 py-4">
              <a
                href="#features"
                onClick={() => setIsMobileMenuOpen(false)}
                className="block rounded-lg px-4 py-3 text-base font-medium text-stone-600 transition-colors hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800"
              >
                Features
              </a>
              <a
                href="#pricing"
                onClick={() => setIsMobileMenuOpen(false)}
                className="block rounded-lg px-4 py-3 text-base font-medium text-stone-600 transition-colors hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800"
              >
                Pricing
              </a>
              <a
                href="#faq"
                onClick={() => setIsMobileMenuOpen(false)}
                className="block rounded-lg px-4 py-3 text-base font-medium text-stone-600 transition-colors hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800"
              >
                FAQ
              </a>
              <a
                href="https://github.com/mikha08-rgb/VoiceLite"
                target="_blank"
                rel="noopener noreferrer"
                className="block rounded-lg px-4 py-3 text-base font-medium text-stone-600 transition-colors hover:bg-stone-100 dark:text-stone-400 dark:hover:bg-stone-800"
              >
                GitHub
              </a>
              <a
                href="#pricing"
                onClick={() => setIsMobileMenuOpen(false)}
                className="block rounded-lg bg-blue-600 px-4 py-3 text-center text-base font-semibold text-white shadow-md shadow-blue-600/30 transition-all hover:bg-blue-700"
              >
                Get Pro
              </a>
            </div>
          </div>
        )}
      </nav>

      {/* Hero Section */}
      <section className="px-6 py-24 md:py-32">
        <div className="container mx-auto max-w-7xl">
          <div className="grid items-center gap-16 md:grid-cols-2">
            {/* Left Column */}
            <div className="space-y-8">
              <h1 className="text-5xl font-bold leading-tight tracking-tight text-stone-900 dark:text-stone-50 md:text-6xl">
                Stop Typing.
                <br />
                Start Speaking.
              </h1>
              <p className="text-xl leading-relaxed text-stone-600 dark:text-stone-400">
                VoiceLite turns your voice into text instantly—anywhere on Windows. Private, fast, and{' '}
                <strong className="font-semibold text-blue-600 dark:text-blue-400">$20 one-time</strong>. No subscription.
              </p>
              <div className="flex flex-col gap-4 sm:flex-row">
                <a
                  href="#pricing"
                  className="inline-flex items-center justify-center gap-2 rounded-lg bg-blue-600 px-8 py-4 text-base font-semibold text-white shadow-lg shadow-blue-600/30 transition-all hover:-translate-y-0.5 hover:bg-blue-700 hover:shadow-xl hover:shadow-blue-600/40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 motion-reduce:transform-none"
                >
                  <Download className="h-5 w-5" />
                  Get VoiceLite Pro - $20
                </a>
                <a
                  href="#features"
                  className="inline-flex items-center justify-center rounded-lg border-2 border-stone-300 bg-transparent px-8 py-4 text-base font-semibold text-stone-700 transition-all hover:border-blue-600 hover:text-blue-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:border-stone-700 dark:text-stone-300 dark:hover:border-blue-400 dark:hover:text-blue-400"
                >
                  Learn More
                </a>
              </div>
              <div className="flex flex-wrap items-center gap-5 text-sm text-stone-500 dark:text-stone-400">
                <span className="flex items-center gap-2">🔒 100% Offline</span>
                <span className="flex items-center gap-2">⚡ Locally Processed</span>
                <span className="flex items-center gap-2">🛡️ Zero Tracking</span>
              </div>
            </div>

            {/* Right Column - Video */}
            <div className="space-y-4">
              <div className="relative aspect-video overflow-hidden rounded-2xl border border-stone-200 bg-gradient-to-br from-blue-50 to-indigo-50 shadow-2xl shadow-blue-600/10 dark:border-stone-800 dark:from-blue-950/50 dark:to-indigo-950/50">
                <div className="flex h-full items-center justify-center text-7xl">▶️</div>
              </div>
              <p className="text-center text-sm text-stone-500 dark:text-stone-400">Watch 60-second demo</p>
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section id="features" className="border-t border-stone-200 px-6 py-24 dark:border-stone-800 md:py-32">
        <div className="container mx-auto max-w-7xl space-y-16">
          <div className="space-y-4 text-center">
            <h2 className="text-4xl font-bold text-stone-900 dark:text-stone-50 md:text-5xl">Built for Developers & Writers</h2>
            <p className="mx-auto max-w-2xl text-xl text-stone-600 dark:text-stone-400">Simple, powerful, and respects your privacy</p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            <div className="space-y-4 rounded-2xl bg-white p-10 shadow-md transition-all hover:-translate-y-2 hover:shadow-xl dark:bg-stone-900">
              <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-gradient-to-br from-blue-50 to-blue-100 text-3xl dark:from-blue-950/50 dark:to-blue-900/50">
                🔒
              </div>
              <h3 className="text-2xl font-semibold text-stone-900 dark:text-stone-50">Privacy First</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">
                No cloud, no tracking, fully offline. Your voice is processed locally on your machine. What you say stays on your device.
              </p>
            </div>

            <div className="space-y-4 rounded-2xl bg-white p-10 shadow-md transition-all hover:-translate-y-2 hover:shadow-xl dark:bg-stone-900">
              <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-gradient-to-br from-blue-50 to-blue-100 text-3xl dark:from-blue-950/50 dark:to-blue-900/50">
                ⚡
              </div>
              <h3 className="text-2xl font-semibold text-stone-900 dark:text-stone-50">Lightning Fast</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">
                &lt;200ms latency after speech. Optimized AI models run locally on GPU. No network delays, no waiting.
              </p>
            </div>

            <div className="space-y-4 rounded-2xl bg-white p-10 shadow-md transition-all hover:-translate-y-2 hover:shadow-xl dark:bg-stone-900">
              <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-gradient-to-br from-blue-50 to-blue-100 text-3xl dark:from-blue-950/50 dark:to-blue-900/50">
                💻
              </div>
              <h3 className="text-2xl font-semibold text-stone-900 dark:text-stone-50">Works Anywhere</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">
                VS Code, Chrome, Discord, Slack, terminals. Any Windows app. Global hotkey works system-wide.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Founder Story */}
      <section className="border-y border-stone-200 bg-stone-50 px-6 py-24 dark:border-stone-800 dark:bg-stone-900/50 md:py-32">
        <div className="container mx-auto max-w-5xl">
          <div className="space-y-8 rounded-2xl bg-white p-12 shadow-md dark:bg-stone-900">
            <p className="text-center text-sm font-bold uppercase tracking-widest text-stone-500 dark:text-stone-400">Why I Built VoiceLite</p>
            <p className="text-center text-2xl leading-relaxed text-stone-700 dark:text-stone-300 md:text-3xl">
              "I got tired of slow, cloud-based dictation tools that tracked everything I said. I wanted something private, fast, and offline.
              Something I could trust with my code, my ideas, my writing. So I built VoiceLite."
            </p>
            <div className="flex items-center justify-center gap-4">
              <div className="flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-indigo-600 text-2xl font-bold text-white">
                M
              </div>
              <div>
                <h4 className="font-semibold text-stone-900 dark:text-stone-50">Misha</h4>
                <p className="text-sm text-stone-600 dark:text-stone-400">Creator of VoiceLite</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* How It Works */}
      <section className="px-6 py-24 md:py-32">
        <div className="container mx-auto max-w-7xl space-y-16">
          <div className="space-y-4 text-center">
            <h2 className="text-4xl font-bold text-stone-900 dark:text-stone-50 md:text-5xl">How VoiceLite Works</h2>
            <p className="mx-auto max-w-2xl text-xl text-stone-600 dark:text-stone-400">Get started in under 2 minutes</p>
          </div>

          <div className="grid gap-12 md:grid-cols-3">
            <div className="space-y-6 text-center">
              <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-indigo-600 text-3xl font-bold text-white">
                1
              </div>
              <h3 className="text-xl font-semibold text-stone-900 dark:text-stone-50">Download & Install</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">98MB download, 2-minute setup, no sign-up required</p>
            </div>

            <div className="space-y-6 text-center">
              <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-indigo-600 text-3xl font-bold text-white">
                2
              </div>
              <h3 className="text-xl font-semibold text-stone-900 dark:text-stone-50">Hold Hotkey & Speak</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">Press Left Alt, say what you want to type, release</p>
            </div>

            <div className="space-y-6 text-center">
              <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-blue-600 to-indigo-600 text-3xl font-bold text-white">
                3
              </div>
              <h3 className="text-xl font-semibold text-stone-900 dark:text-stone-50">Text Appears Instantly</h3>
              <p className="leading-relaxed text-stone-600 dark:text-stone-400">AI transcribes locally, injects text where you type</p>
            </div>
          </div>
        </div>
      </section>

      {/* Model Comparison */}
      <section className="border-t border-stone-200 bg-stone-50 px-6 py-24 dark:border-stone-800 dark:bg-stone-900/50 md:py-32">
        <div className="container mx-auto max-w-7xl space-y-16">
          <div className="space-y-4 text-center">
            <h2 className="text-4xl font-bold text-stone-900 dark:text-stone-50 md:text-5xl">Choose Your AI Model</h2>
            <p className="mx-auto max-w-2xl text-xl text-stone-600 dark:text-stone-400">All 5 models included with your purchase</p>
          </div>

          <div className="overflow-x-auto rounded-2xl bg-white shadow-md dark:bg-stone-900">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b border-stone-200 bg-stone-50 dark:border-stone-800 dark:bg-stone-800/50">
                  <th className="px-6 py-4 text-left text-sm font-semibold uppercase tracking-wider text-stone-700 dark:text-stone-300">Model</th>
                  <th className="px-6 py-4 text-left text-sm font-semibold uppercase tracking-wider text-stone-700 dark:text-stone-300">Size</th>
                  <th className="px-6 py-4 text-left text-sm font-semibold uppercase tracking-wider text-stone-700 dark:text-stone-300">Accuracy</th>
                  <th className="px-6 py-4 text-left text-sm font-semibold uppercase tracking-wider text-stone-700 dark:text-stone-300">Speed</th>
                  <th className="px-6 py-4 text-left text-sm font-semibold uppercase tracking-wider text-stone-700 dark:text-stone-300">Best For</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b border-stone-200 dark:border-stone-800">
                  <td className="px-6 py-4 text-stone-900 dark:text-stone-50">Tiny</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">75MB</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">80-85%</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">1.5s</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">Quick notes, fast drafts</td>
                </tr>
                <tr className="border-b border-stone-200 dark:border-stone-800">
                  <td className="px-6 py-4 text-stone-900 dark:text-stone-50">Swift</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">142MB</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">85-88%</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">2.0s</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">Emails, chat messages</td>
                </tr>
                <tr className="border-b border-stone-200 bg-gradient-to-br from-blue-50 to-indigo-50 dark:border-stone-800 dark:from-blue-950/30 dark:to-indigo-950/30">
                  <td className="px-6 py-4 font-semibold text-stone-900 dark:text-stone-50">Pro ⭐ (Recommended)</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">466MB</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">90-93%</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">2.5s</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">Code, technical writing</td>
                </tr>
                <tr className="border-b border-stone-200 dark:border-stone-800">
                  <td className="px-6 py-4 text-stone-900 dark:text-stone-50">Elite</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">1.5GB</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">93-96%</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">4.0s</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">Articles, reports</td>
                </tr>
                <tr>
                  <td className="px-6 py-4 text-stone-900 dark:text-stone-50">Ultra</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">2.9GB</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">96-98%</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">6.0s</td>
                  <td className="px-6 py-4 text-stone-600 dark:text-stone-400">Professional content</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </section>

      {/* Pricing */}
      <section id="pricing" className="px-6 py-24 md:py-32">
        <div className="container mx-auto max-w-7xl space-y-16">
          <div className="space-y-4 text-center">
            <h2 className="text-4xl font-bold text-stone-900 dark:text-stone-50 md:text-5xl">Simple, Honest Pricing</h2>
            <p className="mx-auto max-w-2xl text-xl text-stone-600 dark:text-stone-400">Try free with Tiny model. Upgrade to Pro for best accuracy.</p>
          </div>

          <div className="mx-auto grid max-w-5xl gap-8 md:grid-cols-2">
            {/* Free Tier */}
            <div className="space-y-8 rounded-3xl border-2 border-stone-200 bg-white p-10 shadow-lg dark:border-stone-800 dark:bg-stone-900">
              <div className="space-y-2 text-center">
                <h3 className="text-2xl font-bold text-stone-900 dark:text-stone-50">Free</h3>
                <p className="text-stone-600 dark:text-stone-400">Get started at no cost</p>
              </div>

              <div className="text-center">
                <div className="text-5xl font-bold text-stone-900 dark:text-stone-50">$0</div>
                <p className="mt-2 text-base text-stone-600 dark:text-stone-400">forever</p>
              </div>

              <ul className="space-y-3">
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  Tiny model (80-85% accuracy)
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  100% offline & private
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  Unlimited usage
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  No time limits
                </li>
              </ul>

              <a
                href="/api/download?version=1.1.0"
                className="block w-full rounded-lg border-2 border-blue-600 bg-transparent px-8 py-4 text-center text-lg font-semibold text-blue-600 transition-all hover:bg-blue-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:border-blue-400 dark:text-blue-400 dark:hover:bg-blue-950/20"
              >
                Download Free
              </a>
            </div>

            {/* Pro Tier */}
            <div className="relative space-y-8 rounded-3xl border-2 border-blue-600 bg-white p-10 shadow-xl dark:border-blue-400 dark:bg-stone-900">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 rounded-full bg-blue-600 px-4 py-1 text-sm font-semibold text-white dark:bg-blue-400 dark:text-blue-950">
                RECOMMENDED
              </div>

              <div className="space-y-2 text-center">
                <h3 className="text-2xl font-bold text-stone-900 dark:text-stone-50">Pro</h3>
                <p className="text-stone-600 dark:text-stone-400">Best accuracy & all features</p>
              </div>

              <div className="text-center">
                <div className="text-5xl font-bold text-blue-600 dark:text-blue-400">$20</div>
                <p className="mt-2 text-base text-stone-600 dark:text-stone-400">one-time payment</p>
              </div>

              <ul className="space-y-3">
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  <strong>All 4 Pro models</strong> (Small/Base/Medium/Large)
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  90-98% accuracy (vs 80-85% free)
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  Lifetime updates
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  100% offline & private
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  Unlimited usage
                </li>
                <li className="flex items-center gap-3 text-stone-700 dark:text-stone-300">
                  <span className="text-lg text-green-600 dark:text-green-400">✓</span>
                  Commercial use allowed
                </li>
              </ul>

              <button
                onClick={handleGetProClick}
                disabled={isCheckoutLoading}
                className="block w-full rounded-lg bg-blue-600 px-8 py-4 text-center text-lg font-semibold text-white shadow-lg shadow-blue-600/30 transition-all hover:bg-blue-700 hover:shadow-xl hover:shadow-blue-600/40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isCheckoutLoading ? 'Loading...' : 'Get Pro - $20'}
              </button>

              <div className="space-y-4 border-t border-stone-200 pt-8 text-center dark:border-stone-800">
                <div className="inline-flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-2 text-sm font-semibold text-green-900 dark:border-green-800 dark:bg-green-950/50 dark:text-green-100">
                  <span className="text-lg">✓</span>
                  30-Day Money-Back Guarantee
                </div>
                <p className="text-sm text-stone-600 dark:text-stone-400">
                  Not happy? Full refund, no questions asked.
                  <br />
                  Secure checkout via Stripe.
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* FAQ */}
      <section id="faq" className="border-t border-stone-200 bg-stone-50 px-6 py-24 dark:border-stone-800 dark:bg-stone-900/50 md:py-32">
        <div className="container mx-auto max-w-4xl space-y-12">
          <div className="space-y-4 text-center">
            <h2 className="text-4xl font-bold text-stone-900 dark:text-stone-50 md:text-5xl">Frequently Asked Questions</h2>
            <p className="mx-auto max-w-2xl text-xl text-stone-600 dark:text-stone-400">Everything you need to know about VoiceLite</p>
          </div>

          <div className="space-y-4">
            {[
              {
                q: 'Does VoiceLite require an internet connection?',
                a: 'No. VoiceLite runs 100% offline on your PC. Your voice never leaves your computer - no internet required for transcription.',
              },
              {
                q: 'Which Windows apps does VoiceLite work with?',
                a: 'All of them! VS Code, Chrome, Discord, Slack, terminals, Word, and any Windows app with a text field. The global hotkey works system-wide.',
              },
              {
                q: 'How accurate is VoiceLite with technical terms?',
                a: 'VoiceLite recognizes technical terms like useState, npm, git, Docker with 90-93% accuracy (Pro model). It handles code, jargon, and specialized vocabulary.',
              },
              {
                q: 'Can I use VoiceLite for coding?',
                a: 'Yes! VoiceLite works great for dictating code. It recognizes function names, variable names, and technical syntax. Many developers use it daily.',
              },
              {
                q: 'Is VoiceLite stable enough for daily use?',
                a: 'Yes! VoiceLite has been in development for months with extensive testing. The core features are production-ready. 30-day money-back guarantee if anything goes wrong.',
              },
              {
                q: 'What languages does VoiceLite support?',
                a: 'VoiceLite supports 99 languages including English, Spanish, French, German, Chinese, Japanese, and many more. All work 100% offline.',
              },
              {
                q: 'Will this slow down my computer?',
                a: 'No. VoiceLite uses <100MB RAM when idle and <300MB when transcribing. It only uses CPU/GPU when you hold the hotkey. Minimal performance impact.',
              },
              {
                q: "What's your refund policy?",
                a: "30-day money-back guarantee on all purchases. If VoiceLite doesn't work for you, email support for a full refund - no questions asked.",
              },
            ].map((item, i) => (
              <div
                key={i}
                className="cursor-pointer rounded-xl border border-stone-200 bg-white p-7 transition-all hover:shadow-md dark:border-stone-800 dark:bg-stone-900"
                onClick={() => setOpenFaqIndex(openFaqIndex === i ? null : i)}
              >
                <div className="flex items-start justify-between gap-4">
                  <h3 className="text-lg font-semibold text-stone-900 dark:text-stone-50">{item.q}</h3>
                  <span className={`text-stone-400 transition-transform ${openFaqIndex === i ? 'rotate-90' : ''}`}>▶</span>
                </div>
                {openFaqIndex === i && (
                  <p className="mt-4 text-base leading-relaxed text-stone-600 dark:text-stone-400">{item.a}</p>
                )}
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Final CTA */}
      <section className="bg-gradient-to-br from-blue-600 to-indigo-600 px-6 py-24 text-white md:py-32">
        <div className="container mx-auto max-w-4xl space-y-10 text-center">
          <h2 className="text-4xl font-bold md:text-5xl">Ready to stop typing?</h2>
          <p className="text-xl opacity-90">Be among the first to experience truly private voice-to-text.</p>
          <a
            href="#pricing"
            className="inline-flex items-center gap-2 rounded-lg bg-white px-10 py-5 text-xl font-semibold text-blue-600 shadow-2xl transition-all hover:scale-105 hover:shadow-3xl focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white motion-reduce:transform-none"
          >
            Get VoiceLite Pro - $20
          </a>
          <p className="text-sm opacity-75">
            Windows 10/11 • 98MB • 2-minute setup
            <br />
            30-day money-back guarantee • Try free tier first
          </p>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-stone-200 bg-white px-6 py-16 dark:border-stone-800 dark:bg-stone-950">
        <div className="container mx-auto max-w-7xl">
          <div className="grid gap-12 md:grid-cols-4">
            <div className="space-y-4">
              <Link href="/" className="flex items-center gap-2 text-xl font-bold text-blue-600 dark:text-blue-400">
                🎤 <span>VoiceLite</span>
              </Link>
              <p className="text-sm leading-relaxed text-stone-600 dark:text-stone-400">
                Private, offline speech-to-text for Windows. Built for developers and writers who value privacy.
              </p>
            </div>

            <div className="space-y-4">
              <h4 className="font-semibold text-stone-900 dark:text-stone-50">Product</h4>
              <div className="flex flex-col gap-3 text-sm">
                <a href="#features" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  Features
                </a>
                <a href="#pricing" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  Pricing
                </a>
                <a
                  href="/api/download?version=1.1.0"
                  className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400"
                >
                  Download
                </a>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="font-semibold text-stone-900 dark:text-stone-50">Resources</h4>
              <div className="flex flex-col gap-3 text-sm">
                <a
                  href="https://github.com/mikha08-rgb/VoiceLite"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400"
                >
                  GitHub
                </a>
                <a href="#faq" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  FAQ
                </a>
                <a href="/docs" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  Documentation
                </a>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="font-semibold text-stone-900 dark:text-stone-50">Legal</h4>
              <div className="flex flex-col gap-3 text-sm">
                <Link href="/privacy" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  Privacy Policy
                </Link>
                <Link href="/terms" className="text-stone-600 transition-colors hover:text-blue-600 dark:text-stone-400 dark:hover:text-blue-400">
                  Terms of Service
                </Link>
              </div>
            </div>
          </div>

          <div className="mt-12 border-t border-stone-200 pt-8 text-center text-sm text-stone-600 dark:border-stone-800 dark:text-stone-400">
            © {new Date().getFullYear()} VoiceLite. All rights reserved.
          </div>
        </div>
      </footer>
    </main>
  );
}
