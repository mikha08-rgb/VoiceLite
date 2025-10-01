'use client';

import { useRef } from 'react';
import { Lock, Package, Mail, Send } from 'lucide-react';
import { CopyButton } from './copy-button';
import { EmptyState } from './empty-state';

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

interface AccountCardProps {
  user: User | null;
  licenses: License[];
  email: string;
  otp: string;
  statusMessage: string | null;
  errorMessage: string | null;
  magicLinkRequested: boolean;
  isPending: boolean;
  onEmailChange: (email: string) => void;
  onOtpChange: (otp: string) => void;
  onMagicLinkRequest: () => void;
  onOtpVerification: () => void;
  onLogout: () => void;
  onLicenseCopy?: () => void;
}

export function AccountCard({
  user,
  licenses,
  email,
  otp,
  statusMessage,
  errorMessage,
  magicLinkRequested,
  isPending,
  onEmailChange,
  onOtpChange,
  onMagicLinkRequest,
  onOtpVerification,
  onLogout,
  onLicenseCopy,
}: AccountCardProps) {
  const emailInputRef = useRef<HTMLInputElement>(null);

  const statusMessageId = statusMessage ? 'account-status-message' : undefined;
  const errorMessageId = errorMessage ? 'account-error-message' : undefined;
  const otpErrorId = errorMessage && magicLinkRequested ? 'otp-error-message' : undefined;
  const describedBy = [statusMessageId, errorMessageId].filter(Boolean).join(' ') || undefined;

  return (
    <div className="rounded-3xl border border-stone-200 bg-white p-10 shadow-sm dark:border-stone-800 dark:bg-stone-900/50 dark:shadow-none">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <span className="flex h-11 w-11 items-center justify-center rounded-full bg-gradient-to-br from-purple-50 to-violet-50 text-purple-600 dark:from-purple-950/50 dark:to-violet-950/50 dark:text-purple-400">
            <Lock className="h-5 w-5" aria-hidden="true" />
          </span>
          <h2 className="text-lg font-bold leading-6 dark:text-stone-50">Your Account</h2>
        </div>
        {user && (
          <button
            onClick={onLogout}
            className="text-sm font-semibold text-stone-600 transition duration-200 hover:text-purple-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:text-stone-300 dark:hover:text-purple-400"
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
          className="mt-6 rounded-xl border border-emerald-200 bg-emerald-50 px-5 py-4 text-sm leading-6 text-emerald-900 dark:border-emerald-600 dark:bg-emerald-950 dark:text-emerald-100"
        >
          {statusMessage}
        </div>
      )}
      {errorMessage && (
        <div
          id={errorMessageId}
          role="alert"
          aria-live="assertive"
          className="mt-6 rounded-xl border border-rose-200 bg-rose-50 px-5 py-4 text-sm leading-6 text-rose-900 dark:border-rose-600 dark:bg-rose-950 dark:text-rose-100"
        >
          {errorMessage}
        </div>
      )}

      <div className="mt-8 space-y-6" aria-busy={isPending ? 'true' : 'false'}>
        <label
          className="block text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400"
          htmlFor="email"
        >
          Email address
        </label>
        <input
          ref={emailInputRef}
          id="email"
          type="email"
          value={email}
          onChange={(event) => onEmailChange(event.target.value)}
          className="w-full rounded-xl border border-stone-200 bg-white px-4 py-3.5 text-sm leading-6 text-stone-900 transition duration-200 focus:border-purple-500 focus:ring-2 focus:ring-purple-500/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 placeholder:text-stone-400 disabled:bg-stone-50 disabled:text-stone-500 motion-reduce:transition-none dark:border-stone-700 dark:bg-stone-900/50 dark:text-stone-100 dark:placeholder:text-stone-500 dark:disabled:bg-stone-800"
          placeholder="you@example.com"
          disabled={!!user}
          aria-invalid={Boolean(errorMessage) && !user}
          aria-describedby={describedBy}
        />

        {!user && (
          <button
            onClick={onMagicLinkRequest}
            disabled={isPending || !email}
            className="group w-full rounded-xl bg-gradient-to-br from-purple-600 to-violet-600 px-4 py-3.5 text-sm font-semibold text-white shadow-md shadow-purple-500/20 transition-all duration-200 hover:shadow-lg hover:shadow-purple-500/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 disabled:cursor-not-allowed disabled:from-stone-300 disabled:to-stone-300 disabled:text-stone-500 disabled:shadow-none motion-reduce:transition-none dark:shadow-purple-500/10 dark:hover:shadow-purple-500/20 dark:disabled:from-stone-700 dark:disabled:to-stone-700"
          >
            {isPending ? (
              <span className="inline-flex items-center gap-2">
                <Send className="h-4 w-4 animate-pulse" aria-hidden="true" />
                Sending...
              </span>
            ) : (
              <span className="inline-flex items-center gap-2">
                <Mail className="h-4 w-4 transition-transform duration-200 group-hover:scale-110" aria-hidden="true" />
                Email me a magic link
              </span>
            )}
          </button>
        )}

        {!user && magicLinkRequested && (
          <div className="space-y-5 animate-in slide-in-from-top-2 fade-in duration-300">
            <div className="flex items-center gap-3 rounded-xl border border-blue-200 bg-blue-50 px-4 py-3 dark:border-blue-800 dark:bg-blue-950/30">
              <Mail className="h-5 w-5 flex-shrink-0 text-blue-600 dark:text-blue-400" aria-hidden="true" />
              <p className="text-sm leading-6 text-blue-900 dark:text-blue-100">
                Check your inbox at <span className="font-semibold">{email}</span> for your magic link or code.
              </p>
            </div>
            <label
              className="block text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400"
              htmlFor="otp"
            >
              Enter 8-digit code from email
            </label>
            <input
              id="otp"
              inputMode="numeric"
              maxLength={8}
              value={otp}
              onChange={(event) => onOtpChange(event.target.value.replace(/[^0-9]/g, ''))}
              className="w-full rounded-xl border border-stone-200 bg-white px-4 py-3.5 text-center font-mono text-lg tracking-[0.48em] text-stone-900 transition duration-200 focus:border-purple-500 focus:ring-2 focus:ring-purple-500/20 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transition-none dark:border-stone-700 dark:bg-stone-900/50 dark:text-stone-100"
              placeholder="12345678"
              aria-describedby={[describedBy, otpErrorId].filter(Boolean).join(' ') || undefined}
              aria-invalid={Boolean(errorMessage)}
            />
            <button
              onClick={onOtpVerification}
              disabled={isPending || otp.length !== 8}
              className="w-full rounded-xl border border-purple-600 bg-gradient-to-br from-purple-600 to-violet-600 px-4 py-3.5 text-sm font-semibold text-white shadow-md shadow-purple-500/20 transition-all duration-200 hover:shadow-lg hover:shadow-purple-500/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 disabled:cursor-not-allowed disabled:border-stone-300 disabled:from-stone-300 disabled:to-stone-300 disabled:text-stone-500 disabled:shadow-none motion-reduce:transition-none dark:border-purple-500 dark:shadow-purple-500/10 dark:hover:shadow-purple-500/20 dark:disabled:border-stone-700 dark:disabled:from-stone-700 dark:disabled:to-stone-700"
            >
              {isPending ? 'Verifying...' : 'Verify code'}
            </button>
          </div>
        )}

        {user && (
          <div className="rounded-2xl border border-stone-200 bg-stone-50 px-6 py-5 dark:border-stone-700 dark:bg-stone-900/30">
            <p className="text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400">
              Signed in as
            </p>
            <p className="mt-2 text-base font-semibold leading-6 text-stone-900 dark:text-stone-50">{user.email}</p>

            {licenses.length > 0 ? (
              <div className="mt-6 space-y-4">
                <p className="text-xs font-bold uppercase tracking-[0.28em] text-stone-500 dark:text-stone-400">
                  Active Licenses
                </p>
                <ul className="space-y-3">
                  {licenses.map((license) => (
                    <li
                      key={license.id}
                      className="rounded-xl border border-stone-200 bg-white px-4 py-4 dark:border-stone-700 dark:bg-stone-800/50"
                    >
                      <div className="flex items-start justify-between gap-4">
                        <code className="flex-1 break-all font-mono text-sm font-bold text-stone-800 dark:text-stone-100">
                          {license.licenseKey}
                        </code>
                        <CopyButton text={license.licenseKey} onCopy={onLicenseCopy} />
                      </div>
                      <div className="mt-4 flex flex-wrap gap-3 text-xs">
                        <span className="rounded-full bg-stone-100 px-3 py-1.5 font-semibold uppercase tracking-wide text-stone-700 dark:bg-stone-800 dark:text-stone-300">
                          {license.type.toLowerCase()}
                        </span>
                        <span className="rounded-full bg-emerald-100 px-3 py-1.5 font-semibold uppercase tracking-wide text-emerald-700 dark:bg-emerald-900 dark:text-emerald-200">
                          {license.status.toLowerCase()}
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              <div className="mt-6">
                <EmptyState
                  icon={Package}
                  title="No licenses yet"
                  description="Choose a plan below to unlock premium features and get your license key instantly via email."
                />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
