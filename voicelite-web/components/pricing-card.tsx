'use client';

import { useState } from 'react';
import { Check, CreditCard, Shield, RotateCcw } from 'lucide-react';
import { RippleButton } from './ripple-button';
import { TrustBadge } from './trust-badge';

interface PricingCardProps {
  id: string;
  name: string;
  description: string;
  price: string;
  popular: boolean;
  bullets: string[];
  comingSoon?: boolean;
  onCheckout: (planId: string) => Promise<void>;
}

export function PricingCard({
  id,
  name,
  description,
  price,
  popular,
  bullets,
  comingSoon = false,
  onCheckout,
}: PricingCardProps) {
  const [isLoading, setIsLoading] = useState(false);

  const handleCheckout = async () => {
    setIsLoading(true);
    try {
      await onCheckout(id);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <article
      className={`relative flex h-full flex-col justify-between gap-10 rounded-3xl border bg-white p-10 shadow-md transition-all duration-300 hover:-translate-y-1 hover:shadow-2xl motion-reduce:transform-none motion-reduce:transition-none dark:bg-stone-900/30 ${
        popular
          ? 'border-purple-500/50 shadow-purple-500/10 dark:border-purple-500/50 dark:shadow-purple-500/20'
          : 'border-stone-200 hover:border-purple-300 dark:border-stone-800 dark:hover:border-purple-800'
      }`}
    >
      {popular && (
        <span className="absolute -top-4 left-8 rounded-full bg-gradient-to-r from-purple-600 to-violet-600 px-4 py-2 text-xs font-bold uppercase tracking-[0.24em] text-white shadow-lg shadow-purple-500/30">
          Most Popular
        </span>
      )}

      <div className="space-y-8">
        <div className="space-y-3">
          <h3 className="text-2xl font-bold leading-tight md:text-3xl">{name}</h3>
          <p className="text-sm leading-6 text-stone-600 dark:text-stone-400">{description}</p>
        </div>

        <div className="space-y-2">
          <p className="text-5xl font-bold leading-none md:text-6xl">{price.split(' ')[0]}</p>
          <p className="text-sm leading-6 text-stone-500 dark:text-stone-400">
            {price.split(' ').slice(1).join(' ')}
          </p>
        </div>

        <ul className="space-y-3 text-sm leading-6 text-stone-600 dark:text-stone-400">
          {bullets.map((bullet) => (
            <li key={bullet} className="flex items-start gap-4">
              <span className="mt-0.5 inline-flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br from-purple-50 to-violet-50 text-purple-600 shadow-sm shadow-purple-100/50 dark:from-purple-950/50 dark:to-violet-950/50 dark:text-purple-400 dark:shadow-purple-500/10">
                <Check className="h-4 w-4" aria-hidden="true" />
              </span>
              <span className="flex-1 font-medium">{bullet}</span>
            </li>
          ))}
        </ul>

        <div className="flex flex-wrap gap-2 pt-2">
          <TrustBadge icon={Shield} text="30-day money back" />
          <TrustBadge icon={RotateCcw} text="Cancel anytime" />
        </div>
      </div>

      {comingSoon ? (
        <div className="space-y-3">
          <div className="rounded-xl border-2 border-dashed border-purple-300 bg-purple-50 px-6 py-4 text-center dark:border-purple-800 dark:bg-purple-950/30">
            <p className="text-sm font-semibold text-purple-900 dark:text-purple-300">
              🚀 Launching in 2-3 weeks
            </p>
            <p className="mt-1 text-xs text-purple-700 dark:text-purple-400">
              Payment infrastructure being finalized
            </p>
          </div>
        </div>
      ) : (
        <RippleButton
          onClick={handleCheckout}
          disabled={isLoading}
          className={`inline-flex w-full items-center justify-center rounded-xl px-6 py-4 text-base font-semibold shadow-md transition-all duration-300 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 disabled:cursor-not-allowed disabled:opacity-50 motion-reduce:transition-none ${
            popular
              ? 'bg-gradient-to-br from-purple-600 to-violet-600 text-white shadow-purple-500/25 hover:scale-[1.02] hover:shadow-lg hover:shadow-purple-500/30 motion-reduce:transform-none dark:shadow-purple-500/20'
              : 'border-2 border-purple-200 bg-white text-purple-700 hover:border-purple-300 hover:bg-purple-50 dark:border-purple-800 dark:bg-stone-900/50 dark:text-purple-300 dark:hover:border-purple-700'
          }`}
          rippleColor={popular ? 'rgba(255, 255, 255, 0.4)' : 'rgba(124, 58, 237, 0.3)'}
        >
          <CreditCard className="mr-2 h-5 w-5" aria-hidden="true" />
          {isLoading ? 'Redirecting...' : 'Get Started'}
        </RippleButton>
      )}
    </article>
  );
}
