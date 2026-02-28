interface Option {
  value: string;
  label: string;
}

interface Props {
  value: string;
  options: readonly Option[];
  onChange: (value: string) => void;
  disabled?: boolean;
}

export function Select({ value, options, onChange, disabled }: Props) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      className="
        bg-[var(--bg-tertiary)] text-[var(--text-primary)] text-sm
        border border-[var(--border)] rounded-md px-3 py-1.5
        focus:outline-none focus:ring-1 focus:ring-[var(--accent)]
        disabled:opacity-50 disabled:cursor-not-allowed
        cursor-pointer min-w-[140px]
      "
    >
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );
}
