'use client';

import { useEffect, useState, useTransition } from 'react';
import { Check, CreditCard, Download, Lock, Mic, Shield, Zap, X } from 'lucide-react';

interface User {
  id: string;
  email: string;
}

interface License {
  id: string;
  licenseKey: string;
  type: 'SUBSCRIPTION' | 'LIFETIME';
  status: 'ACTIVE' | 'CANCELED' | 'EXPIRED';
  expiresAt: string | null;
}

const plans = [
  {
    id: 'quarterly',
    name: 'Quarterly',
    description: 'Full access billed every 3 months',
    price: '$20 / 3 months',
    priceId: 'quarterly',
    popular: true,
    bullets: ['All Whisper models', 'Priority support', 'Automatic updates'],
  },
  {
    id: 'lifetime',
    name: 'Lifetime',
    description: 'One-time payment, lifetime updates',
    price: '$99 one-time',
    priceId: 'lifetime',
    popular: false,
    bullets: ['Permanent license', 'All future updates', 'Priority support'],
  },
];

export default function Home() {
  const [user, setUser] = useState<User | null>(null);
  const [licenses, setLicenses] = useState<License[]>([]);
  const [email, setEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [magicLinkRequested, setMagicLinkRequested] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [isCheckoutLoading, setIsCheckoutLoading] = useState<string | null>(null);

  const fetchProfile = async () => {
    try {
      const response = await fetch('/api/me', { cache: 'no-store' });
      if (!response.ok) {
        setUser(null);
        setLicenses([]);
        return;
      }
      const data = await response.json();
      setUser(data.user);
      setLicenses(data.licenses ?? []);
      if (data.user) {
        setEmail(data.user.email);
      }
    } catch (error) {
      console.error('Failed to load profile', error);
    }
  };

  useEffect(() => {
    fetchProfile();
  }, []);

  const handleMagicLinkRequest = async () => {
    setErrorMessage(null);
    setStatusMessage(null);
    startTransition(async () => {
      try {
        const response = await fetch('/api/auth/request', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email }),
        });
        if (!response.ok) {
          const payload = await response.json();
          throw new Error(payload.error ?? 'Failed to send magic link');
        }
        setMagicLinkRequested(true);
        setStatusMessage('Check your email for the magic link or enter the OTP below.');
      } catch (error) {
        console.error(error);
        setErrorMessage(error instanceof Error ? error.message : 'Failed to send magic link');
      }
    });
  };

  const handleOtpVerification = async () => {
    setErrorMessage(null);
    setStatusMessage(null);
    startTransition(async () => {
      try {
        const response = await fetch('/api/auth/otp', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email, otp }),
        });
        if (!response.ok) {
          const payload = await response.json();
          throw new Error(payload.error ?? 'Invalid code');
        }
        setOtp('');
        setMagicLinkRequested(false);
        setStatusMessage('You are signed in.');
        await fetchProfile();
      } catch (error) {
        console.error(error);
        setErrorMessage(error instanceof Error ? error.message : 'Failed to verify code');
      }
    });
  };

  const handleLogout = async () => {
    await fetch('/api/auth/logout', { method: 'POST' });
    setUser(null);
    setLicenses([]);
    setMagicLinkRequested(false);
    setStatusMessage('Signed out.');
  };

  const handleCheckout = async (plan: 'quarterly' | 'lifetime') => {
    if (!user) {
      setErrorMessage('Please sign in before upgrading.');
      return;
    }

    setIsCheckoutLoading(plan);
    try {
      const response = await fetch('/api/checkout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ plan }),
      });
      const data = await response.json();
      if (!response.ok || !data.url) {
        throw new Error(data.error ?? 'Checkout failed');
      }
      window.location.href = data.url;
    } catch (error) {
      console.error('Checkout error:', error);
      setErrorMessage(error instanceof Error ? error.message : 'Unable to start checkout');
    } finally {
      setIsCheckoutLoading(null);
    }
  };

  return (
    <main className="min-h-screen bg-gradient-to-b from-gray-50 to-white">
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
              href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.8/VoiceLite-Setup-1.0.8.exe"
              download
              className="inline-flex items-center px-8 py-4 text-lg font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              <Download className="w-5 h-5 mr-2" />
              Download VoiceLite Free
            </a>
          </div>

          <p className="mt-4 text-sm text-gray-500">
            Windows 10/11 • One-click installer • Ready in 2 minutes
          </p>

          <div className="mt-8 max-w-lg mx-auto bg-white border border-gray-200 rounded-xl shadow-sm p-6 text-left">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">
                <Lock className="inline w-5 h-5 mr-2 text-blue-600" /> Account
              </h2>
              {user && (
                <button
                  onClick={handleLogout}
                  className="text-sm text-red-500 hover:text-red-600"
                >
                  Sign out
                </button>
              )}
            </div>

            {statusMessage && <p className="text-sm text-green-600 mb-3">{statusMessage}</p>}
            {errorMessage && <p className="text-sm text-red-600 mb-3">{errorMessage}</p>}

            <div className="space-y-3">
              <label className="block text-sm font-medium text-gray-700" htmlFor="email">
                Email address
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                className="w-full rounded-lg border border-gray-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="you@example.com"
                disabled={!!user}
              />

              {!user && (
                <button
                  onClick={handleMagicLinkRequest}
                  disabled={isPending || !email}
                  className="w-full rounded-lg bg-blue-600 text-white font-semibold py-2 px-4 hover:bg-blue-700 transition disabled:opacity-50"
                >
                  {isPending ? 'Sending...' : 'Email me a magic link'}
                </button>
              )}

              {!user && magicLinkRequested && (
                <div className="space-y-2">
                  <label className="block text-sm font-medium text-gray-700" htmlFor="otp">
                    One-time code
                  </label>
                  <input
                    id="otp"
                    inputMode="numeric"
                    maxLength={8}
                    value={otp}
                    onChange={(event) => setOtp(event.target.value.replace(/[^0-9]/g, ''))}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    placeholder="12345678"
                  />
                  <button
                    onClick={handleOtpVerification}
                    disabled={isPending || otp.length !== 8}
                    className="w-full rounded-lg bg-gray-900 text-white font-semibold py-2 px-4 hover:bg-gray-800 transition disabled:opacity-50"
                  >
                    {isPending ? 'Verifying...' : 'Verify code'}
                  </button>
                </div>
              )}

              {user && (
                <div className="border border-blue-100 bg-blue-50 rounded-lg p-4">
                  <p className="text-sm text-blue-900">
                    Signed in as <strong>{user.email}</strong>
                  </p>
                  {licenses.length > 0 ? (
                    <ul className="mt-3 space-y-2 text-sm text-blue-800">
                      {licenses.map((license) => (
                        <li key={license.id}>
                          License <code className="font-mono">{license.licenseKey}</code> — {license.type.toLowerCase()} ({license.status.toLowerCase()})
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="mt-3 text-sm text-blue-700">No licenses linked yet. Choose a plan below.</p>
                  )}
                </div>
              )}
            </div>
          </div>

          <div className="mt-6 p-4 bg-amber-50 border border-amber-200 rounded-lg max-w-2xl mx-auto">
            <p className="text-sm text-amber-800">
              <strong>⚠️ Important:</strong> If you get a "missing DLL" error, download{' '}
              <a
                href="https://aka.ms/vs/17/release/vc_redist.x64.exe"
                className="underline font-semibold hover:text-amber-900"
                target="_blank"
                rel="noopener noreferrer"
              >
                VC++ Runtime
              </a>
              {' '}(13MB, 30 seconds) then run VoiceLite again.
            </p>
          </div>
        </div>
      </section>

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

      <section className="px-6 py-20 bg-gray-50">
        <div className="max-w-5xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-4">
            Upgrade When You&apos;re Ready
          </h2>
          <p className="text-center text-gray-600 mb-12">
            Sign in, choose your plan, and we&apos;ll email the license instantly.
          </p>

          <div className="grid md:grid-cols-2 gap-8">
            {plans.map((plan) => (
              <div
                key={plan.id}
                className={`rounded-2xl border ${plan.popular ? 'border-blue-300 bg-white shadow-xl' : 'border-gray-200 bg-white shadow-sm'} p-8 flex flex-col justify-between`}
              >
                <div>
                  <div className="flex items-center justify-between">
                    <div>
                      <h3 className="text-2xl font-semibold text-gray-900">{plan.name}</h3>
                      <p className="text-gray-600 mt-1">{plan.description}</p>
                    </div>
                    {plan.popular && (
                      <span className="text-xs font-semibold uppercase tracking-wide text-blue-600 bg-blue-100 px-3 py-1 rounded-full">
                        Recommended
                      </span>
                    )}
                  </div>

                  <p className="text-3xl font-bold text-gray-900 mt-6">{plan.price}</p>

                  <ul className="mt-6 space-y-3 text-sm text-gray-700">
                    {plan.bullets.map((bullet) => (
                      <li key={bullet} className="flex items-center">
                        <Check className="w-4 h-4 text-blue-600 mr-2" />
                        {bullet}
                      </li>
                    ))}
                  </ul>
                </div>

                <button
                  onClick={() => handleCheckout(plan.id as 'quarterly' | 'lifetime')}
                  disabled={isCheckoutLoading === plan.id}
                  className={`mt-8 inline-flex items-center justify-center rounded-lg px-5 py-3 text-sm font-semibold transition ${
                    plan.popular
                      ? 'bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-60'
                      : 'bg-gray-900 text-white hover:bg-gray-800 disabled:opacity-60'
                  }`}
                >
                  <CreditCard className="w-4 h-4 mr-2" />
                  {isCheckoutLoading === plan.id ? 'Redirecting…' : 'Upgrade now'}
                </button>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="px-6 py-20">
        <div className="max-w-6xl mx-auto">
          <h2 className="text-3xl font-bold text-center text-gray-900 mb-12">
            Everything You Need for Frictionless Dictation
          </h2>
          <div className="grid md:grid-cols-3 gap-8">
            {[
              {
                title: 'Offline by Design',
                description: 'No account needed to start. Upgrade when you want model packs and priority support.',
              },
              {
                title: 'Magical Login',
                description: 'Passwordless. We email you a magic link + OTP that works on both the desktop app and web.',
              },
              {
                title: 'Real Licensing',
                description: 'Purchases create managed licenses tied to your account. Activate up to 3 devices instantly.',
              },
            ].map((feature) => (
              <div key={feature.title} className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm">
                <h3 className="text-xl font-semibold text-gray-900 mb-2">{feature.title}</h3>
                <p className="text-gray-600 text-sm">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>
    </main>
  );
}