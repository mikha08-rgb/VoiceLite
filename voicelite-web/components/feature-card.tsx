import { LucideIcon } from 'lucide-react';
import { ReactNode } from 'react';

interface FeatureCardProps {
  icon: LucideIcon;
  title: string;
  description: string | ReactNode;
}

export function FeatureCard({ icon: Icon, title, description }: FeatureCardProps) {
  return (
    <article className="group flex flex-col gap-5 rounded-3xl border border-stone-200 bg-white p-8 transition-all duration-300 hover:-translate-y-1 hover:shadow-xl hover:shadow-purple-500/10 motion-reduce:transform-none motion-reduce:transition-none dark:border-stone-800 dark:bg-stone-900/30 dark:hover:border-purple-500/30 dark:hover:shadow-purple-500/20">
      <span className="inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-purple-50 to-violet-50 text-purple-600 shadow-sm shadow-purple-100/50 transition-transform duration-300 group-hover:scale-105 motion-reduce:transform-none dark:from-purple-950/50 dark:to-violet-950/50 dark:text-purple-400 dark:shadow-purple-500/10">
        <Icon className="h-7 w-7" aria-hidden="true" />
      </span>
      <h3 className="text-xl font-bold leading-6">{title}</h3>
      <p className="text-sm leading-[1.7] text-stone-600 dark:text-stone-400">{description}</p>
    </article>
  );
}
