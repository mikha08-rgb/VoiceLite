import { ReactNode } from "react";

interface Props {
  title: string;
  children: ReactNode;
}

export function SettingsGroup({ title, children }: Props) {
  return (
    <div className="mb-6">
      <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--text-secondary)] mb-2 px-1">
        {title}
      </h3>
      <div className="bg-[var(--bg-secondary)] rounded-lg border border-[var(--border)] divide-y divide-[var(--border)] px-3">
        {children}
      </div>
    </div>
  );
}
