import { useId } from 'react';

export interface LanguageOption {
  value: string;
  label: string;
}

export interface LanguageSelectProps {
  label: string;
  value: string;
  options: readonly LanguageOption[];
  onChange: (value: string) => void;
}

export function LanguageSelect({ label, value, options, onChange }: LanguageSelectProps) {
  const id = useId();
  return (
    <div className="flex items-center gap-2 text-sm">
      <label htmlFor={id} className="text-neutral-600">
        {label}
      </label>
      <select
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="rounded-md border border-neutral-300 bg-white px-2 py-1"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </div>
  );
}
