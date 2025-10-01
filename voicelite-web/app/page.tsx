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
    <main className="min-h-screen bg-gradient-to-br from-gray-50 via-blue-50/30 to-white">
      <section className="px-6 py-16 md:py-24 text-center">
        <div className="max-w-5xl mx-auto">
          <div className="inline-flex items-center px-4 py-2 mb-8 text-sm font-medium text-blue-700 bg-blue-50 border border-blue-100 rounded-full shadow-sm">
            <Mic className="w-4 h-4 mr-2" />
            100% Offline Voice Typing for Windows
          </div>

          <h1 className="text-5xl md:text-7xl font-bold text-gray-900 mb-6 leading-tight">
            Turn Your Voice Into Text
            <br />
            <span className="bg-gradient-to-r from-blue-600 to-blue-500 bg-clip-text text-transparent">Instantly</span>
          </h1>

          <p className="text-lg md:text-xl text-gray-600 mb-10 max-w-2xl mx-auto leading-relaxed">
            Hold Alt, speak naturally, release. Your words appear as typed text in <span className="font-semibold text-gray-900">any</span> Windows application.
            No internet required. Your voice never leaves your PC.
          </p>

          <div className="flex flex-col sm:flex-row gap-4 justify-center mb-6">
            <a
              href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.8/VoiceLite-Setup-1.0.8.exe"
              download
              className="group inline-flex items-center justify-center px-8 py-4 text-lg font-semibold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-all shadow-lg hover:shadow-xl hover:scale-[1.02]"
            >
              <Download className="w-5 h-5 mr-2 group-hover:animate-bounce" />
              Download VoiceLite Free
            </a>
          </div>

          <p className="text-sm text-gray-500 flex items-center justify-center gap-2">
            <span>Windows 10/11</span>
            <span className="text-gray-300">•</span>
            <span>One-click installer</span>
            <span className="text-gray-300">•</span>
            <span>Ready in 2 minutes</span>
          </p>

          <div className="mt-12 max-w-lg mx-auto bg-white border border-gray-200 rounded-2xl shadow-lg p-6 text-left">
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-lg font-semibold text-gray-900 flex items-center">
                <div className="w-10 h-10 rounded-full bg-blue-50 flex items-center justify-center mr-3">
                  <Lock className="w-5 h-5 text-blue-600" />
                </div>
                Your Account
              </h2>
              {user && (
                <button
                  onClick={handleLogout}
                  className="text-sm text-red-600 hover:text-red-700 font-medium transition-colors"
                >
                  Sign out
                </button>
              )}
            </div>

            {statusMessage && (
              <div className="mb-4 p-3 bg-green-50 border border-green-200 rounded-lg">
                <p className="text-sm text-green-700 font-medium">{statusMessage}</p>
              </div>
            )}
            {errorMessage && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg">
                <p className="text-sm text-red-700 font-medium">{errorMessage}</p>
              </div>
            )}

            <div className="space-y-3">
              <label className="block text-sm font-medium text-gray-700" htmlFor="email">
                Email address
              </label>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                className="w-full rounded-lg border-2 border-gray-200 px-4 py-3 focus:outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 transition-all disabled:bg-gray-50 disabled:text-gray-500"
                placeholder="you@example.com"
                disabled={!!user}
              />

              {!user && (
                <button
                  onClick={handleMagicLinkRequest}
                  disabled={isPending || !email}
                  className="w-full rounded-lg bg-blue-600 text-white font-semibold py-3 px-4 hover:bg-blue-700 transition-all shadow-md hover:shadow-lg disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isPending ? 'Sending...' : 'Email me a magic link'}
                </button>
              )}

              {!user && magicLinkRequested && (
                <div className="space-y-3 pt-2">
                  <label className="block text-sm font-medium text-gray-700" htmlFor="otp">
                    Enter 8-digit code from email
                  </label>
                  <input
                    id="otp"
                    inputMode="numeric"
                    maxLength={8}
                    value={otp}
                    onChange={(event) => setOtp(event.target.value.replace(/[^0-9]/g, ''))}
                    className="w-full rounded-lg border-2 border-gray-200 px-4 py-3 text-center text-lg tracking-widest font-mono focus:outline-none focus:border-gray-900 focus:ring-2 focus:ring-gray-100 transition-all"
                    placeholder="12345678"
                  />
                  <button
                    onClick={handleOtpVerification}
                    disabled={isPending || otp.length !== 8}
                    className="w-full rounded-lg bg-gray-900 text-white font-semibold py-3 px-4 hover:bg-gray-800 transition-all shadow-md hover:shadow-lg disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isPending ? 'Verifying...' : 'Verify code'}
                  </button>
                </div>
              )}

              {user && (
                <div className="border-2 border-blue-100 bg-gradient-to-br from-blue-50 to-blue-50/50 rounded-xl p-5">
                  <p className="text-sm text-blue-900 font-medium mb-1">
                    Signed in as
                  </p>
                  <p className="text-base text-blue-950 font-semibold mb-3">{user.email}</p>
                  {licenses.length > 0 ? (
                    <div className="mt-4 pt-4 border-t border-blue-200">
                      <p className="text-xs font-semibold text-blue-700 uppercase tracking-wide mb-3">Active Licenses</p>
                      <ul className="space-y-3">
                        {licenses.map((license) => (
                          <li key={license.id} className="bg-white rounded-lg p-3 border border-blue-100">
                            <code className="text-sm font-mono text-gray-900 font-semibold">{license.licenseKey}</code>
                            <div className="flex gap-2 mt-2 text-xs">
                              <span className="px-2 py-1 bg-blue-100 text-blue-700 rounded font-medium">
                                {license.type.toLowerCase()}
                              </span>
                              <span className="px-2 py-1 bg-green-100 text-green-700 rounded font-medium">
                                {license.status.toLowerCase()}
                              </span>
                            </div>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ) : (
                    <div className="mt-4 pt-4 border-t border-blue-200">
                      <p className="text-sm text-blue-700">No licenses yet. Choose a plan below to get started!</p>
                    </div>
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

      <section className="px-6 py-16 md:py-24 bg-white">
        <div className="max-w-6xl mx-auto">
          <h2 className="text-3xl md:text-4xl font-bold text-center text-gray-900 mb-4">
            Why VoiceLite?
          </h2>
          <p className="text-center text-gray-600 mb-16 max-w-2xl mx-auto">
            Built for privacy, speed, and universal compatibility
          </p>

          <div className="grid md:grid-cols-3 gap-8 md:gap-10">
            <div className="text-center group">
              <div className="inline-flex items-center justify-center w-20 h-20 mb-6 text-blue-600 bg-gradient-to-br from-blue-50 to-blue-100 rounded-2xl shadow-sm group-hover:shadow-md transition-all">
                <Shield className="w-10 h-10" />
              </div>
              <h3 className="text-xl font-bold mb-3 text-gray-900">100% Private</h3>
              <p className="text-gray-600 leading-relaxed">
                Completely offline. Your voice never leaves your computer. No cloud, no tracking, no data collection.
              </p>
            </div>

            <div className="text-center group">
              <div className="inline-flex items-center justify-center w-20 h-20 mb-6 text-green-600 bg-gradient-to-br from-green-50 to-green-100 rounded-2xl shadow-sm group-hover:shadow-md transition-all">
                <Zap className="w-10 h-10" />
              </div>
              <h3 className="text-xl font-bold mb-3 text-gray-900">Lightning Fast</h3>
              <p className="text-gray-600 leading-relaxed">
                Instant transcription with less than 200ms latency from speech to text. No waiting, no buffering.
              </p>
            </div>

            <div className="text-center group">
              <div className="inline-flex items-center justify-center w-20 h-20 mb-6 text-purple-600 bg-gradient-to-br from-purple-50 to-purple-100 rounded-2xl shadow-sm group-hover:shadow-md transition-all">
                <Mic className="w-10 h-10" />
              </div>
              <h3 className="text-xl font-bold mb-3 text-gray-900">Works Everywhere</h3>
              <p className="text-gray-600 leading-relaxed">
                Discord, VS Code, Word, Terminal, Games—any Windows app, any text field, anywhere.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="px-6 py-16 md:py-24 bg-gradient-to-b from-gray-50 to-white">
        <div className="max-w-5xl mx-auto">
          <h2 className="text-3xl md:text-4xl font-bold text-center text-gray-900 mb-4">
            Upgrade When You&apos;re Ready
          </h2>
          <p className="text-center text-gray-600 mb-14 max-w-2xl mx-auto">
            Sign in, choose your plan, and get your license key instantly via email
          </p>

          <div className="grid md:grid-cols-2 gap-6 md:gap-8">
            {plans.map((plan) => (
              <div
                key={plan.id}
                className={`relative rounded-3xl border-2 ${
                  plan.popular
                    ? 'border-blue-400 bg-white shadow-2xl scale-[1.02]'
                    : 'border-gray-200 bg-white shadow-lg'
                } p-8 flex flex-col justify-between transition-all hover:shadow-2xl hover:scale-[1.02]`}
              >
                {plan.popular && (
                  <div className="absolute -top-4 left-1/2 -translate-x-1/2">
                    <span className="inline-block px-4 py-1.5 text-xs font-bold uppercase tracking-wider text-white bg-gradient-to-r from-blue-600 to-blue-500 rounded-full shadow-lg">
                      Most Popular
                    </span>
                  </div>
                )}

                <div>
                  <div className="mb-6">
                    <h3 className="text-2xl md:text-3xl font-bold text-gray-900 mb-2">{plan.name}</h3>
                    <p className="text-gray-600">{plan.description}</p>
                  </div>

                  <div className="mb-8">
                    <p className="text-4xl md:text-5xl font-bold text-gray-900">{plan.price.split(' ')[0]}</p>
                    <p className="text-gray-500 mt-1">{plan.price.split(' ').slice(1).join(' ')}</p>
                  </div>

                  <ul className="space-y-4 mb-8">
                    {plan.bullets.map((bullet) => (
                      <li key={bullet} className="flex items-start">
                        <div className="flex-shrink-0 w-5 h-5 rounded-full bg-blue-100 flex items-center justify-center mt-0.5 mr-3">
                          <Check className="w-3 h-3 text-blue-600" />
                        </div>
                        <span className="text-gray-700">{bullet}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                <button
                  onClick={() => handleCheckout(plan.id as 'quarterly' | 'lifetime')}
                  disabled={isCheckoutLoading === plan.id}
                  className={`w-full inline-flex items-center justify-center rounded-xl px-6 py-4 text-base font-bold transition-all shadow-lg hover:shadow-xl ${
                    plan.popular
                      ? 'bg-blue-600 text-white hover:bg-blue-700 hover:scale-[1.02] disabled:opacity-60'
                      : 'bg-gray-900 text-white hover:bg-gray-800 hover:scale-[1.02] disabled:opacity-60'
                  } disabled:cursor-not-allowed`}
                >
                  <CreditCard className="w-5 h-5 mr-2" />
                  {isCheckoutLoading === plan.id ? 'Redirecting…' : 'Get Started'}
                </button>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="px-6 py-16 md:py-20 bg-white">
        <div className="max-w-6xl mx-auto">
          <h2 className="text-3xl md:text-4xl font-bold text-center text-gray-900 mb-4">
            Everything You Need for Frictionless Dictation
          </h2>
          <p className="text-center text-gray-600 mb-12 max-w-2xl mx-auto">
            Simple, secure, and powerful voice typing for everyone
          </p>
          <div className="grid md:grid-cols-3 gap-6 md:gap-8">
            {[
              {
                title: 'Offline by Design',
                description: 'No account needed to start. Upgrade when you want model packs and priority support.',
              },
              {
                title: 'Passwordless Login',
                description: 'Secure magic link + OTP authentication that works on both desktop and web.',
              },
              {
                title: 'Multi-Device Licensing',
                description: 'Activate your license on up to 3 devices. Managed from your account dashboard.',
              },
            ].map((feature) => (
              <div key={feature.title} className="bg-gradient-to-br from-gray-50 to-white border-2 border-gray-100 rounded-2xl p-6 shadow-sm hover:shadow-md transition-all">
                <h3 className="text-xl font-bold text-gray-900 mb-3">{feature.title}</h3>
                <p className="text-gray-600 leading-relaxed">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <footer className="bg-gray-900 text-gray-300 px-6 py-12">
        <div className="max-w-6xl mx-auto">
          <div className="grid md:grid-cols-4 gap-8 mb-8">
            <div className="md:col-span-2">
              <div className="flex items-center mb-4">
                <Mic className="w-6 h-6 text-blue-500 mr-2" />
                <span className="text-xl font-bold text-white">VoiceLite</span>
              </div>
              <p className="text-sm text-gray-400 leading-relaxed">
                Privacy-focused offline voice typing for Windows. Your voice never leaves your computer.
              </p>
            </div>

            <div>
              <h4 className="text-sm font-semibold text-white mb-3 uppercase tracking-wider">Product</h4>
              <ul className="space-y-2 text-sm">
                <li>
                  <a href="https://github.com/mikha08-rgb/VoiceLite" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                    Download
                  </a>
                </li>
                <li>
                  <a href="https://github.com/mikha08-rgb/VoiceLite#features" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                    Features
                  </a>
                </li>
                <li>
                  <a href="https://github.com/mikha08-rgb/VoiceLite/releases" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                    Changelog
                  </a>
                </li>
              </ul>
            </div>

            <div>
              <h4 className="text-sm font-semibold text-white mb-3 uppercase tracking-wider">Support</h4>
              <ul className="space-y-2 text-sm">
                <li>
                  <a href="https://github.com/mikha08-rgb/VoiceLite/issues" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                    Report Issue
                  </a>
                </li>
                <li>
                  <a href="https://github.com/mikha08-rgb/VoiceLite#readme" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                    Documentation
                  </a>
                </li>
              </ul>
            </div>
          </div>

          <div className="pt-8 border-t border-gray-800 flex flex-col md:flex-row justify-between items-center gap-4">
            <p className="text-sm text-gray-500">
              © {new Date().getFullYear()} VoiceLite. Open source under MIT License.
            </p>
            <div className="flex gap-6 text-sm">
              <a href="https://github.com/mikha08-rgb/VoiceLite" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                GitHub
              </a>
              <a href="https://github.com/mikha08-rgb/VoiceLite/blob/master/LICENSE" className="hover:text-white transition-colors" target="_blank" rel="noopener noreferrer">
                License
              </a>
            </div>
          </div>
        </div>
      </footer>
    </main>
  );
}