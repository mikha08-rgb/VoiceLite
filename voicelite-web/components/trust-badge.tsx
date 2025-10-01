import { LucideIcon } from 'lucide-react';

interface TrustBadgeProps {
  icon: LucideIcon;
  text: string;
}

export function TrustBadge({ icon: Icon, text }: TrustBadgeProps) {
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-300">
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      <span>{text}</span>
    </div>
  );
}
