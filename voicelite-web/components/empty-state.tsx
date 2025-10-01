import { LucideIcon } from 'lucide-react';

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description: string;
  action?: {
    label: string;
    onClick: () => void;
  };
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center rounded-2xl border-2 border-dashed border-stone-200 bg-stone-50/50 px-6 py-12 text-center animate-in fade-in duration-500 dark:border-stone-700 dark:bg-stone-900/20">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-purple-50 to-violet-50 text-purple-600 animate-in zoom-in duration-500 delay-75 dark:from-purple-950/50 dark:to-violet-950/50 dark:text-purple-400">
        <Icon className="h-8 w-8" aria-hidden="true" />
      </div>
      <h3 className="mt-6 text-base font-bold text-stone-900 dark:text-stone-50">{title}</h3>
      <p className="mt-2 max-w-sm text-sm leading-6 text-stone-600 dark:text-stone-400">{description}</p>
      {action && (
        <button
          onClick={action.onClick}
          className="mt-6 rounded-lg bg-gradient-to-br from-purple-600 to-violet-600 px-4 py-2 text-sm font-semibold text-white shadow-md shadow-purple-500/20 transition-all duration-200 hover:scale-105 hover:shadow-lg hover:shadow-purple-500/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500 motion-reduce:transform-none motion-reduce:transition-none dark:shadow-purple-500/10"
        >
          {action.label}
        </button>
      )}
    </div>
  );
}
