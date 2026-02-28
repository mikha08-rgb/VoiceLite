import { ButtonHTMLAttributes, ReactNode } from "react";

interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger";
  children: ReactNode;
}

const variants = {
  primary:
    "bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-white",
  secondary:
    "bg-[var(--bg-tertiary)] hover:bg-[var(--border)] text-[var(--text-primary)] border border-[var(--border)]",
  danger:
    "bg-[var(--error)] hover:bg-red-600 text-white",
};

export function Button({
  variant = "primary",
  children,
  className = "",
  ...props
}: Props) {
  return (
    <button
      className={`
        px-4 py-1.5 rounded-md text-sm font-medium
        transition-colors duration-150
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--accent)]
        disabled:opacity-50 disabled:cursor-not-allowed
        ${variants[variant]} ${className}
      `}
      {...props}
    >
      {children}
    </button>
  );
}
