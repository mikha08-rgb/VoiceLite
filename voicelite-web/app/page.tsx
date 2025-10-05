'use client';

import { useEffect, useRef, useState, useTransition, lazy, Suspense } from 'react';
import { Download, Mic, Shield, Zap } from 'lucide-react';
import { ThemeToggle } from '@/components/theme-toggle';
import { FeatureCard } from '@/components/feature-card';
import { LoadingSkeleton } from '@/components/loading-skeleton';
import { ToastContainer } from '@/components/toast';
import { useToast } from '@/hooks/use-toast';
import { RippleButton } from '@/components/ripple-button';
import { Tooltip } from '@/components/tooltip';

// Lazy load below-the-fold components
const PricingCard = lazy(() => import('@/components/pricing-card').then(mod => ({ default: mod.PricingCard })));
const AccountCard = lazy(() => import('@/components/account-card').then(mod => ({ default: mod.AccountCard })));
const FAQ = lazy(() => import('@/components/faq').then(mod => ({ default: mod.FAQ })));

// Lazy load confetti only when needed
const loadConfetti = () => import('canvas-confetti');

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
    comingSoon: true, // Added: Pro tier launching soon
  },
  {
    id: 'lifetime',
    name: 'Lifetime',
    description: 'One-time payment, lifetime updates',
    price: '$99 one-time',
    priceId: 'lifetime',
    popular: false,
    bullets: ['Permanent license', 'All future updates', 'Priority support'],
    comingSoon: true, // Added: Pro tier launching soon
  },
];

const faqItems = [
  {
    question: 'Is my voice data sent to the cloud?',
    answer: 'No. VoiceLite runs 100% offline on your PC using local Whisper AI models. Your voice never leaves your computer - no internet connection required for transcription.',
  },
  {
    question: 'When will Pro tier launch?',
    answer: 'Pro tier is launching in 2-3 weeks! We\'re finalizing payment infrastructure (LLC setup and Stripe integration). The free tier with Tiny model is fully functional now. Pro will unlock premium models (base, small, medium) for 95%+ accuracy on technical terms.',
  },
  {
    question: "What's the difference between free and Pro?",
    answer: 'The free version includes the tiny Whisper model (fastest, basic accuracy). Pro unlocks all premium models (base, small, medium) for higher accuracy and better performance on technical terms.',
  },
  {
    question: 'Which Whisper model should I use?',
    answer: 'Start with tiny (included free) for speed. Upgrade to base or small for better accuracy. Medium is best for technical jargon but uses more RAM. All models run offline.',
  },
  {
    question: 'Does it work in games, Discord, VS Code?',
    answer: 'Yes! VoiceLite works in any Windows application with a text field - browsers, IDEs, terminals, chat apps, and even games in windowed mode. Just hold your hotkey and speak.',
  },
  {
    question: 'Can I cancel my subscription anytime?',
    answer: 'Absolutely. Cancel anytime from your account dashboard. No questions asked, no fees. Your license remains active until the end of your billing period.',
  },
  {
    question: 'How accurate is the transcription?',
    answer: 'Accuracy depends on the model: tiny (85-90%), base (90-93%), small (93-95%), medium (95-97%). Technical terms and code (useState, npm, git) are recognized well with larger models.',
  },
  {
    question: 'Can I use VoiceLite on multiple PCs?',
    answer: 'Yes! Each license activates on up to 3 devices. Manage activations from your account dashboard. Deactivate old devices to free up slots.',
  },
  {
    question: 'Is there a refund policy?',
    answer: '30-day money-back guarantee on all purchases. If VoiceLite doesn\'t work for you, email support for a full refund - no questions asked.',
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
  const [isLoading, setIsLoading] = useState(true);

  const signInSectionRef = useRef<HTMLElement>(null);
  const { toasts, showToast, removeToast } = useToast();

  const fetchProfile = async () => {
    setIsLoading(true);
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
    } finally {
      setIsLoading(false);
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

        // Trigger success confetti (lazy loaded)
        loadConfetti().then((confetti) => {
          confetti.default({
            particleCount: 100,
            spread: 70,
            origin: { y: 0.6 },
            colors: ['#7c3aed', '#8b5cf6', '#a78bfa', '#c4b5fd'],
          });
        });
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

  const handleCheckout = async (plan: string) => {
    if (!user) {
      setErrorMessage('Please sign in before upgrading.');

      // Scroll to sign-in section
      if (signInSectionRef.current) {
        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        signInSectionRef.current.scrollIntoView({
          behavior: prefersReducedMotion ? 'auto' : 'smooth',
          block: 'center'
        });
      }

      return;
    }

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
    }
  };

  const handleLicenseCopy = () => {
    showToast('License key copied to clipboard!', 'success');
  };

  return (
    <main id="main-content" className="min-h-screen bg-stone-50 text-stone-900 dark:bg-[#0f0f12] dark:text-stone-50">
      <a href="#main-content" className="skip-to-content">
        Skip to main content
      </a>
      <section className="px-6 py-32 md:py-40">
        <div className="mx-auto max-w-6xl md:grid md:grid-cols-[1.05fr_0.95fr] md:gap-20">
          <header className="max-w-2xl space-y-10">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <span className="inline-flex items-center gap-2 rounded-full border border-purple-200/50 bg-gradient-to-br from-purple-50 to-violet-50 px-4 py-2.5 text-xs font-bold uppercase tracking-[0.24em] text-purple-900 shadow-sm shadow-purple-100/50 dark:border-purple-500/30 dark:from-purple-950/50 dark:to-violet-950/50 dark:text-purple-200 dark:shadow-purple-500/10">
                <Mic className="h-4 w-4 text-purple-600 dark:text-purple-400" aria-hidden="true" />
                100% Offline Voice Typing for Windows
              </span>
              <ThemeToggle />
            </div>
            <div className="space-y-7">
              <h1 className="text-4xl font-bold leading-tight tracking-tight md:text-6xl">
                Turn Your Voice Into Text
                <br />
                <span className="bg-gradient-to-r from-purple-600 via-violet-600 to-purple-600 bg-clip-text text-transparent dark:from-purple-400 dark:via-violet-400 dark:to-purple-400">Instantly</span>
              </h1>
              <p className="text-base leading-[1.7] text-stone-600 dark:text-stone-300 md:text-lg">
                Hold Alt, speak naturally, release. Your words appear as typed text in{' '}
                <span className="font-semibold text-stone-900 dark:text-stone-50">any</span> Windows application.{' '}
                <Tooltip content="Powered by OpenAI Whisper AI - runs 100% on your PC">
                  <span>100% offline</span>
                </Tooltip>
                . Your voice never leaves your PC.
              </p>
            </div>
            <div className="flex flex-col items-stretch gap-5 sm:w-auto sm:flex-row sm:items-center">
              <RippleButton
                onClick={() => {
                  window.location.href = 'https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.45/VoiceLite-Setup-1.0.45.exe';
                }}
                className="group inline-flex w-full items-center justify-center rounded-full bg-gradient-to-br from-purple-600 to-violet-600 px-8 py-3.5 text-base font-semibold text-white shadow-lg shadow-purple-500/25 transition-all duration-300 hover:scale-[1.02] hover:shadow-xl hover:shadow-purple-500/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transform-none motion-reduce:transition-none dark:shadow-purple-500/20 dark:hover:shadow-purple-500/30"
                rippleColor="rgba(255, 255, 255, 0.4)"
              >
                <Download className="mr-2 h-5 w-5 transition-transform duration-300 group-hover:translate-y-0.5" aria-hidden="true" />
                Download VoiceLite Free
              </RippleButton>
            </div>
            <div className="flex items-center gap-4 text-sm text-stone-500 dark:text-stone-400" role="list" aria-label="Download prerequisites">
              <span>Windows 10/11</span>
              <span aria-hidden="true" className="h-1 w-1 rounded-full bg-stone-300 dark:bg-stone-600" />
              <span>One-click installer</span>
              <span aria-hidden="true" className="h-1 w-1 rounded-full bg-stone-300 dark:bg-stone-600" />
              <span>Ready in 2 minutes</span>
            </div>
          </header>

          <aside ref={signInSectionRef} className="mt-20 space-y-8 md:mt-0" aria-label="Account and activation">
            {isLoading ? (
              <LoadingSkeleton />
            ) : (
              <Suspense fallback={<LoadingSkeleton />}>
                <AccountCard
                  user={user}
                  licenses={licenses}
                  email={email}
                  otp={otp}
                  statusMessage={statusMessage}
                  errorMessage={errorMessage}
                  magicLinkRequested={magicLinkRequested}
                  isPending={isPending}
                  onEmailChange={setEmail}
                  onOtpChange={setOtp}
                  onMagicLinkRequest={handleMagicLinkRequest}
                  onOtpVerification={handleOtpVerification}
                  onLogout={handleLogout}
                  onLicenseCopy={handleLicenseCopy}
                />
              </Suspense>
            )}

            <div className="rounded-2xl border border-green-200 bg-gradient-to-br from-green-50 to-emerald-50 px-6 py-6 text-sm leading-6 text-green-900 dark:border-green-800 dark:from-green-950/50 dark:to-emerald-950/50 dark:text-green-100">
              <strong className="font-bold">✨ New in v1.0.45:</strong> VC++ Runtime now auto-installed! No more DLL errors. Works completely offline. Supports Windows 10 & 11 with enhanced 4K display scaling.
            </div>
          </aside>
        </div>
      </section>

      <section className="border-t border-stone-200 bg-white px-6 py-32 dark:border-stone-800 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-6xl space-y-16">
          <div className="space-y-5 text-center">
            <h2 className="text-3xl font-bold leading-tight md:text-4xl">Why VoiceLite?</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-stone-600 dark:text-stone-400">
              Built for privacy, speed, and universal compatibility
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            <FeatureCard
              icon={Shield}
              title="100% Private"
              description="Completely offline. Your voice never leaves your computer. No cloud, no tracking, no data collection."
            />
            <FeatureCard
              icon={Zap}
              title="Lightning Fast"
              description={
                <>
                  Instant transcription with{' '}
                  <Tooltip content="From the moment you stop speaking to text appearing">
                    <span>less than 200ms latency</span>
                  </Tooltip>{' '}
                  from speech to text. No waiting, no buffering.
                </>
              }
            />
            <FeatureCard
              icon={Mic}
              title="Works Everywhere"
              description="Discord, VS Code, Word, Terminal, Games—any Windows app, any text field, anywhere."
            />
          </div>
        </div>
      </section>

      <section className="border-y border-stone-200 bg-stone-100/50 px-6 py-32 dark:border-stone-800 dark:bg-stone-950/50">
        <div className="mx-auto max-w-5xl space-y-16">
          <div className="space-y-5 text-center">
            <h2 className="text-3xl font-bold leading-tight md:text-4xl">Upgrade When You're Ready</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-stone-600 dark:text-stone-400">
              Sign in, choose your plan, and get your license key instantly via email
            </p>
          </div>

          <div className="grid gap-10 md:grid-cols-2">
            <Suspense fallback={<div className="h-96 animate-pulse rounded-3xl bg-stone-200 dark:bg-stone-800" />}>
              {plans.map((plan) => (
                <PricingCard
                  key={plan.id}
                  id={plan.id}
                  name={plan.name}
                  description={plan.description}
                  price={plan.price}
                  popular={plan.popular}
                  bullets={plan.bullets}
                  comingSoon={plan.comingSoon}
                  onCheckout={handleCheckout}
                />
              ))}
            </Suspense>
          </div>
        </div>
      </section>

      <section className="bg-white px-6 py-32 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-6xl space-y-16">
          <div className="space-y-5 text-center">
            <h2 className="text-3xl font-bold leading-tight md:text-4xl">Everything You Need for Frictionless Dictation</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-stone-600 dark:text-stone-400">
              Simple, secure, and powerful voice typing for everyone
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            {[
              {
                icon: Shield,
                title: 'Offline by Design',
                description: 'No account needed to start. Upgrade when you want model packs and priority support.',
              },
              {
                icon: Zap,
                title: 'Passwordless Login',
                description: 'Secure magic link + OTP authentication that works on both desktop and web.',
              },
              {
                icon: Mic,
                title: 'Multi-Device Licensing',
                description: 'Activate your license on up to 3 devices. Managed from your account dashboard.',
              },
            ].map((feature) => (
              <FeatureCard key={feature.title} icon={feature.icon} title={feature.title} description={feature.description} />
            ))}
          </div>
        </div>
      </section>

      <section className="border-y border-stone-200 bg-stone-100/50 px-6 py-32 dark:border-stone-800 dark:bg-stone-950/50">
        <div className="mx-auto max-w-4xl space-y-12">
          <div className="space-y-5 text-center">
            <h2 className="text-3xl font-bold leading-tight md:text-4xl">Frequently Asked Questions</h2>
            <p className="mx-auto max-w-2xl text-base leading-6 text-stone-600 dark:text-stone-400">
              Everything you need to know about VoiceLite
            </p>
          </div>
          <Suspense fallback={<div className="space-y-4">{[...Array(6)].map((_, i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-stone-200 dark:bg-stone-800" />)}</div>}>
            <FAQ items={faqItems} />
          </Suspense>
        </div>
      </section>

      <footer className="border-t border-stone-200 bg-white px-6 py-24 dark:border-stone-800 dark:bg-[#0f0f12]">
        <div className="mx-auto max-w-6xl space-y-12">
          <div className="grid gap-12 md:grid-cols-4">
            <div className="space-y-5 md:col-span-2">
              <div className="flex items-center gap-4">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-purple-600 to-violet-600 shadow-md shadow-purple-500/30">
                  <Mic className="h-5 w-5 text-white" aria-hidden="true" />
                </div>
                <span className="text-lg font-bold leading-6">VoiceLite</span>
              </div>
              <p className="text-sm leading-[1.7] text-stone-600 dark:text-stone-400">
                Privacy-focused offline voice typing for Windows. Your voice never leaves your computer.
              </p>
            </div>

            <nav aria-label="Product links">
              <h4 className="text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400">Product</h4>
              <ul className="mt-4 space-y-3 text-sm leading-6 text-stone-600 dark:text-stone-400">
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Download
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite#features"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Features
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite/releases"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Changelog
                  </a>
                </li>
              </ul>
            </nav>

            <nav aria-label="Support links">
              <h4 className="text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400">Support</h4>
              <ul className="mt-4 space-y-3 text-sm leading-6 text-stone-600 dark:text-stone-400">
                <li>
                  <a
                    href="/feedback"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                  >
                    Send Feedback
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite/issues"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Report Issue
                  </a>
                </li>
                <li>
                  <a
                    href="https://github.com/mikha08-rgb/VoiceLite#readme"
                    className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Documentation
                  </a>
                </li>
              </ul>
            </nav>
          </div>

          <div className="flex flex-col gap-4 border-t border-stone-200 pt-8 text-xs leading-6 text-stone-500 dark:border-stone-800 dark:text-stone-400 md:flex-row md:items-center md:justify-between">
            <p>© {new Date().getFullYear()} VoiceLite. Open source under MIT License.</p>
            <div className="flex gap-6">
              <a
                href="https://github.com/mikha08-rgb/VoiceLite"
                className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub
              </a>
              <a
                href="https://github.com/mikha08-rgb/VoiceLite/blob/master/LICENSE"
                className="transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:hover:text-purple-400"
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

      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </main>
  );
}
