export function LoadingSkeleton() {
  return (
    <div className="rounded-3xl border border-stone-200 bg-white p-10 shadow-sm dark:border-stone-800 dark:bg-stone-900/50 dark:shadow-none">
      {/* Header skeleton */}
      <div className="flex items-center gap-4">
        <div className="h-11 w-11 animate-pulse rounded-full bg-stone-200 dark:bg-stone-700" />
        <div className="h-6 w-32 animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
      </div>

      {/* Form skeleton */}
      <div className="mt-8 space-y-6">
        <div className="h-4 w-24 animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
        <div className="h-12 w-full animate-pulse rounded-xl bg-stone-200 dark:bg-stone-700" />
        <div className="h-12 w-full animate-pulse rounded-xl bg-stone-200 dark:bg-stone-700" />
      </div>
    </div>
  );
}

export function PricingCardSkeleton() {
  return (
    <article className="flex h-full flex-col justify-between gap-10 rounded-3xl border border-stone-200 bg-white p-10 shadow-md dark:bg-stone-900/30">
      <div className="space-y-8">
        <div className="space-y-3">
          <div className="h-8 w-32 animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
          <div className="h-4 w-full animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
        </div>

        <div className="space-y-2">
          <div className="h-12 w-24 animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
          <div className="h-4 w-20 animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
        </div>

        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="flex items-start gap-4">
              <div className="mt-0.5 h-6 w-6 animate-pulse rounded-lg bg-stone-200 dark:bg-stone-700" />
              <div className="h-4 w-full animate-pulse rounded bg-stone-200 dark:bg-stone-700" />
            </div>
          ))}
        </div>
      </div>

      <div className="h-12 w-full animate-pulse rounded-xl bg-stone-200 dark:bg-stone-700" />
    </article>
  );
}
