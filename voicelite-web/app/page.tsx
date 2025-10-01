'use client';

import { useEffect, useRef, useState, useTransition } from 'react';
import { Check, CreditCard, Download, Lock, Mic, Shield, Zap } from 'lucide-react';
import { ThemeToggle } from '@/components/theme-toggle';

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

  const signInSectionRef = useRef<HTMLElement>(null);
  const emailInputRef = useRef<HTMLInputElement>(null);

  const statusMessageId = statusMessage ? 'account-status-message' : undefined;
  const errorMessageId = errorMessage ? 'account-error-message' : undefined;
  const otpErrorId = errorMessage && magicLinkRequested ? 'otp-error-message' : undefined;

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

      // Scroll to sign-in section and focus email input
      if (signInSectionRef.current) {
        // Check if user prefers reduced motion
        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        signInSectionRef.current.scrollIntoView({
          behavior: prefersReducedMotion ? 'auto' : 'smooth',
          block: 'center'
        });

        // Focus email input after scroll completes
        // Use shorter delay for reduced motion, longer for smooth scroll
        const focusDelay = prefersReducedMotion ? 100 : 500;
        setTimeout(() => {
          emailInputRef.current?.focus();
        }, focusDelay);
      }

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

  const describedBy = [statusMessageId, errorMessageId].filter(Boolean).join(' ') || undefined;

  return (
    <main className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      <section className="px-6 py-24 md:py-32">
        <div className="mx-auto max-w-6xl md:grid md:grid-cols-[1.05fr_0.95fr] md:gap-16">
          <header className="max-w-2xl space-y-8">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <span className="inline-flex items-center gap-2 rounded-full border border-zinc-200 bg-white px-4 py-2 text-xs font-semibold uppercase tracking-[0.24em] text-zinc-600 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300">
                <Mic className="h-4 w-4 text-zinc-500 dark:text-zinc-300" aria-hidden="true" />
                100% Offline Voice Typing for Windows
              </span>
              <ThemeToggle />
            </div>
            <div className="space-y-6">
              <h1 className="text-4xl font-semibold leading-tight tracking-tight md:text-6xl">
                Turn Your Voice Into Text
                <br />
                <span className="text-indigo-600">Instantly</span>
              </h1>
              <p className="text-base leading-7 text-zinc-600 dark:text-zinc-300 md:text-lg">
                Hold Alt, speak naturally, release. Your words appear as typed text in{' '}
                <span className="font-semibold text-zinc-900 dark:text-zinc-100">any</span> Windows application. No internet
                required. Your voice never leaves your PC.
              </p>
            </div>
            <div className="flex flex-col items-stretch gap-4 sm:w-auto sm:flex-row sm:items-center">
              <a
                href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.8/VoiceLite-Setup-1.0.8.exe"
                download
                className="inline-flex w-full items-center justify-center rounded-full bg-indigo-600 px-7 py-3 text-base font-medium text-white transition duration-200 hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:bg-indigo-500 dark:hover:bg-indigo-400"
              >
                <Download className="mr-2 h-5 w-5" aria-hidden="true" />
                Download VoiceLite Free
              </a>
            </div>
            <div className="flex items-center gap-4 text-sm text-zinc-500 dark:text-zinc-400" role="list" aria-label="Download prerequisites">
              <span>Windows 10/11</span>
              <span aria-hidden="true" className="h-1 w-1 rounded-full bg-zinc-300 dark:bg-zinc-600" />
              <span>One-click installer</span>
              <span aria-hidden="true" className="h-1 w-1 rounded-full bg-zinc-300 dark:bg-zinc-600" />
              <span>Ready in 2 minutes</span>
            </div>
          </header>

          <aside ref={signInSectionRef} className="mt-16 space-y-8 md:mt-0" aria-label="Account and activation">
            <div className="rounded-2xl border border-zinc-200 bg-white p-8 shadow-sm dark:border-zinc-800 dark:bg-zinc-900 dark:shadow-none">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <span className="flex h-10 w-10 items-center justify-center rounded-full bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                    <Lock className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <h2 className="text-base font-semibold leading-6 dark:text-zinc-100">Your Account</h2>
                </div>
                {user && (
                  <button
                    onClick={handleLogout}
                    className="text-sm font-medium text-zinc-600 transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:text-zinc-300 dark:hover:text-zinc-100"
                  >
                    Sign out
                  </button>
                )}
              </div>

              {statusMessage && (
                <div
                  id={statusMessageId}
                  role="status"
                  aria-live="polite"
                  className="mt-6 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm leading-6 text-emerald-800 dark:border-emerald-600 dark:bg-emerald-950 dark:text-emerald-200"
                >
                  {statusMessage}
                </div>
              )}
              {errorMessage && (
                <div
                  id={errorMessageId}
                  role="alert"
                  aria-live="assertive"
                  className="mt-6 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm leading-6 text-rose-800 dark:border-rose-600 dark:bg-rose-950 dark:text-rose-200"
                >
                  {errorMessage}
                </div>
              )}

              <div className="mt-6 space-y-6" aria-busy={isPending ? 'true' : 'false'}>
                <label className="block text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400" htmlFor="email">
                  Email address
                </label>
                <input
                  ref={emailInputRef}
                  id="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  className="w-full rounded-xl border border-zinc-200 px-4 py-3 text-sm leading-6 text-zinc-900 transition duration-200 focus:border-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 placeholder:text-zinc-400 disabled:bg-zinc-100 disabled:text-zinc-500 motion-reduce:transition-none dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-100 dark:placeholder:text-zinc-500 dark:disabled:bg-zinc-800"
                  placeholder="you@example.com"
                  disabled={!!user}
                  aria-invalid={Boolean(errorMessage) && !user}
                  aria-describedby={describedBy}
                />

                {!user && (
                  <button
                    onClick={handleMagicLinkRequest}
                    disabled={isPending || !email}
                    className="w-full rounded-xl bg-indigo-600 px-4 py-3 text-sm font-medium text-white transition duration-200 hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:bg-zinc-300 disabled:text-zinc-500 motion-reduce:transition-none dark:bg-indigo-500 dark:hover:bg-indigo-400 dark:disabled:bg-zinc-700 dark:disabled:text-zinc-500"
                  >
                    {isPending ? 'Sending...' : 'Email me a magic link'}
                  </button>
                )}

                {!user && magicLinkRequested && (
                  <div className="space-y-4">
                    <label className="block text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400" htmlFor="otp">
                      Enter 8-digit code from email
                    </label>
                    <input
                      id="otp"
                      inputMode="numeric"
                      maxLength={8}
                      value={otp}
                      onChange={(event) => setOtp(event.target.value.replace(/[^0-9]/g, ''))}
                      className="w-full rounded-xl border border-zinc-200 px-4 py-3 text-center font-mono text-lg tracking-[0.48em] text-zinc-900 transition duration-200 focus:border-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-100"
                      placeholder="12345678"
                      aria-describedby={[describedBy, otpErrorId].filter(Boolean).join(' ') || undefined}
                      aria-invalid={Boolean(errorMessage)}
                    />
                    <button
                      onClick={handleOtpVerification}
                      disabled={isPending || otp.length !== 8}
                      className="w-full rounded-xl border border-indigo-600 bg-indigo-600 px-4 py-3 text-sm font-medium text-white transition duration-200 hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:border-zinc-300 disabled:bg-zinc-300 disabled:text-zinc-500 motion-reduce:transition-none dark:border-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400 dark:disabled:border-zinc-700 dark:disabled:bg-zinc-700"
                    >
                      {isPending ? 'Verifying...' : 'Verify code'}
                    </button>
                  </div>
                )}

                {user && (
                  <div className="rounded-2xl border border-zinc-200 bg-zinc-50 px-6 py-4 dark:border-zinc-700 dark:bg-zinc-900">
                    <p className="text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400">Signed in as</p>
                    <p className="mt-2 text-sm font-medium leading-6 text-zinc-900 dark:text-zinc-100">{user.email}</p>

                    {licenses.length > 0 ? (
                      <div className="mt-6 space-y-4">
                        <p className="text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400">Active Licenses</p>
                        <ul className="space-y-2">
                          {licenses.map((license) => (
                            <li key={license.id} className="rounded-xl border border-zinc-200 bg-white px-4 py-3 dark:border-zinc-700 dark:bg-zinc-800">
                              <code className="font-mono text-sm font-semibold text-zinc-800 dark:text-zinc-100">{license.licenseKey}</code>
                              <div className="mt-4 flex flex-wrap gap-4 text-xs">
                                <span className="rounded-full bg-zinc-100 px-3 py-1 font-medium uppercase tracking-wide text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                                  {license.type.toLowerCase()}
                                </span>
                                <span className="rounded-full bg-emerald-100 px-3 py-1 font-medium uppercase tracking-wide text-emerald-700 dark:bg-emerald-900 dark:text-emerald-200">
                                  {license.status.toLowerCase()}
                                </span>
                              </div>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : (
                      <div className="mt-6">
                        <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">No licenses yet. Choose a plan below to get started!</p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>

            <div className="rounded-2xl border border-amber-200 bg-amber-50 px-6 py-6 text-sm leading-6 text-amber-900 dark:border-amber-600 dark:bg-amber-950 dark:text-amber-200">
              <strong>Important:</strong> If you get a "missing DLL" error, download{' '}
              <a
                href="https://aka.ms/vs/17/release/vc_redist.x64.exe"
                className="font-semibold text-indigo-600 underline transition duration-200 hover:text-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:text-indigo-300 dark:hover:text-indigo-200"
                target="_blank"
                rel="noopener noreferrer"
              >
                VC++ Runtime
              </a>
              {' '}(13MB, 30 seconds) then run VoiceLite again.
            </div>
          </aside>
        </div>
      </section>

      <section className="border-t border-zinc-200 bg-white px-6 py-24 dark:border-zinc-800 dark:bg-zinc-950">
        <div className="mx-auto max-w-6xl space-y-12">
          <div className="space-y-4 text-center">
            <h2 className="text-3xl font-semibold leading-tight md:text-4xl">Why VoiceLite?</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-zinc-600 dark:text-zinc-400">
              Built for privacy, speed, and universal compatibility
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            <article className="flex flex-col gap-4 rounded-2xl border border-zinc-200 bg-white p-8 transition duration-200 hover:-translate-y-1 hover:shadow-lg motion-reduce:transform-none motion-reduce:transition-none dark:border-zinc-800 dark:bg-zinc-900 dark:hover:shadow-zinc-900/40">
              <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-indigo-600 dark:bg-indigo-900/40 dark:text-indigo-300">
                <Shield className="h-6 w-6" aria-hidden="true" />
              </span>
              <h3 className="text-lg font-semibold leading-6">100% Private</h3>
              <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                Completely offline. Your voice never leaves your computer. No cloud, no tracking, no data collection.
              </p>
            </article>

            <article className="flex flex-col gap-4 rounded-2xl border border-zinc-200 bg-white p-8 transition duration-200 hover:-translate-y-1 hover:shadow-lg motion-reduce:transform-none motion-reduce:transition-none dark:border-zinc-800 dark:bg-zinc-900 dark:hover:shadow-zinc-900/40">
              <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-indigo-600 dark:bg-indigo-900/40 dark:text-indigo-300">
                <Zap className="h-6 w-6" aria-hidden="true" />
              </span>
              <h3 className="text-lg font-semibold leading-6">Lightning Fast</h3>
              <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                Instant transcription with less than 200ms latency from speech to text. No waiting, no buffering.
              </p>
            </article>

            <article className="flex flex-col gap-4 rounded-2xl border border-zinc-200 bg-white p-8 transition duration-200 hover:-translate-y-1 hover:shadow-lg motion-reduce:transform-none motion-reduce:transition-none dark:border-zinc-800 dark:bg-zinc-900 dark:hover:shadow-zinc-900/40">
              <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100 text-indigo-600 dark:bg-indigo-900/40 dark:text-indigo-300">
                <Mic className="h-6 w-6" aria-hidden="true" />
              </span>
              <h3 className="text-lg font-semibold leading-6">Works Everywhere</h3>
              <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                Discord, VS Code, Word, Terminal, Games—any Windows app, any text field, anywhere.
              </p>
            </article>
          </div>
        </div>
      </section>

      <section className="border-y border-zinc-200 bg-zinc-100/40 px-6 py-24 dark:border-zinc-800 dark:bg-zinc-900/40">
        <div className="mx-auto max-w-5xl space-y-12">
          <div className="space-y-4 text-center">
            <h2 className="text-3xl font-semibold leading-tight md:text-4xl">Upgrade When You're Ready</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-zinc-600 dark:text-zinc-400">
              Sign in, choose your plan, and get your license key instantly via email
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-2">
            {plans.map((plan) => (
              <article
                key={plan.id}
                className={`relative flex h-full flex-col justify-between gap-8 rounded-3xl border bg-white p-8 shadow-sm transition duration-200 focus-within:border-indigo-600 hover:-translate-y-1 hover:shadow-xl motion-reduce:transform-none motion-reduce:transition-none dark:border-zinc-800 dark:bg-zinc-900 dark:hover:shadow-zinc-900/60 ${
                  plan.popular ? 'border-indigo-600 dark:border-indigo-500' : 'border-zinc-200 hover:border-zinc-300 dark:border-zinc-800 dark:hover:border-zinc-700'
                }`}
              >
                {plan.popular && (
                  <span className="absolute -top-4 left-8 rounded-full bg-indigo-600 px-3 py-1 text-xs font-semibold uppercase tracking-[0.24em] text-white dark:bg-indigo-500">
                    Most Popular
                  </span>
                )}

                <div className="space-y-6">
                  <div className="space-y-2">
                    <h3 className="text-2xl font-semibold leading-tight md:text-3xl">{plan.name}</h3>
                    <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">{plan.description}</p>
                  </div>

                  <div className="space-y-2">
                    <p className="text-4xl font-semibold leading-none md:text-5xl">{plan.price.split(' ')[0]}</p>
                    <p className="text-sm leading-6 text-zinc-500 dark:text-zinc-400">{plan.price.split(' ').slice(1).join(' ')}</p>
                  </div>

                  <ul className="space-y-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                    {plan.bullets.map((bullet) => (
                      <li key={bullet} className="flex items-start gap-4">
                        <span className="mt-1 inline-flex h-5 w-5 items-center justify-center rounded-full bg-indigo-600/10 text-indigo-600 dark:bg-indigo-500/20 dark:text-indigo-300">
                          <Check className="h-3 w-3" aria-hidden="true" />
                        </span>
                        <span>{bullet}</span>
                      </li>
                    ))}
                  </ul>
                </div>

                <button
                  onClick={() => handleCheckout(plan.id as 'quarterly' | 'lifetime')}
                  disabled={isCheckoutLoading === plan.id}
                  className={`inline-flex w-full items-center justify-center rounded-xl px-6 py-3 text-sm font-medium transition duration-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:bg-zinc-200 disabled:text-zinc-500 motion-reduce:transition-none dark:disabled:bg-zinc-700 dark:disabled:text-zinc-500 ${
                    plan.popular
                      ? 'bg-indigo-600 text-white hover:bg-indigo-500 dark:bg-indigo-500 dark:hover:bg-indigo-400'
                      : 'border border-zinc-300 bg-white text-zinc-900 hover:border-zinc-400 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-100 dark:hover:border-zinc-600'
                  }`}
                >
                  <CreditCard className="mr-2 h-5 w-5" aria-hidden="true" />
                  {isCheckoutLoading === plan.id ? 'Redirecting.' : 'Get Started'}
                </button>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="bg-white px-6 py-24 dark:bg-zinc-950">
        <div className="mx-auto max-w-6xl space-y-12">
          <div className="space-y-4 text-center">
            <h2 className="text-3xl font-semibold leading-tight md:text-4xl">Everything You Need for Frictionless Dictation</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-zinc-600 dark:text-zinc-400">
              Simple, secure, and powerful voice typing for everyone
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
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
              <article key={feature.title} className="flex flex-col gap-4 rounded-2xl border border-zinc-200 bg-white p-8 transition duration-200 hover:-translate-y-1 hover:shadow-lg motion-reduce:transform-none motion-reduce:transition-none dark:border-zinc-800 dark:bg-zinc-900 dark:hover:shadow-zinc-900/40">
                <h3 className="text-lg font-semibold leading-6">{feature.title}</h3>
                <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">{feature.description}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <footer className="border-t border-zinc-200 bg-white px-6 py-24 dark:border-zinc-800 dark:bg-zinc-950">
        <div className="mx-auto max-w-6xl space-y-10">
          <div className="grid gap-10 md:grid-cols-4">
            <div className="space-y-4 md:col-span-2">
              <div className="flex items-center gap-4">
                <Mic className="h-5 w-5 text-zinc-500 dark:text-zinc-300" aria-hidden="true" />
                <span className="text-base font-semibold leading-6">VoiceLite</span>
              </div>
              <p className="text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                Privacy-focused offline voice typing for Windows. Your voice never leaves your computer.
              </p>
            </div>

            <nav aria-label="Product links">
              <h4 className="text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400">Product</h4>
              <ul className="mt-4 space-y-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite"
                    className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Download
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite#features"
                    className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Features
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite/releases"
                    className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Changelog
                  </a>
                </li>
              </ul>
            </nav>

            <nav aria-label="Support links">
              <h4 className="text-xs font-semibold uppercase tracking-[0.28em] text-zinc-500 dark:text-zinc-400">Support</h4>
              <ul className="mt-4 space-y-2 text-sm leading-6 text-zinc-600 dark:text-zinc-400">
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite/issues"
                    className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Report Issue
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite#readme"
                    className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Documentation
                  </a>
                </li>
              </ul>
            </nav>
          </div>

          <div className="flex flex-col gap-4 border-t border-zinc-200 pt-6 text-xs leading-6 text-zinc-500 dark:border-zinc-800 dark:text-zinc-400 md:flex-row md:items-center md:justify-between">
            <p>© {new Date().getFullYear()} VoiceLite. Open source under MIT License.</p>
            <div className="flex gap-6">
              <a
                href="https://github.com/mikha08-rgb/VoiceLite"
                className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub
              </a>
              <a
                href="https://github.com/mikha08-rgb/VoiceLite/blob/master/LICENSE"
                className="transition duration-200 hover:text-zinc-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 motion-reduce:transition-none dark:hover:text-zinc-100"
                target="_blank"
                rel="noopener noreferrer"
              >
                License
              </a>
            </div>
          </div>
        </div>
      </footer>

      <div aria-live="polite" className="sr-only" id="otp-error-message">
        {magicLinkRequested && errorMessage ? errorMessage : null}
      </div>
    </main>
  );
}
