import { ReactNode } from "react";

interface Props {
  label: string;
  description?: string;
  children: ReactNode;
}

export function SettingContainer({ label, description, children }: Props) {
  return (
    <div className="flex items-center justify-between py-3 px-1">
      <div className="flex-1 mr-4">
        <div className="text-sm font-medium text-[var(--text-primary)]">
          {label}
        </div>
        {description && (
          <div className="text-xs text-[var(--text-secondary)] mt-0.5">
            {description}
          </div>
        )}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  );
}
