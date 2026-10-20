import type { ReactNode } from 'react';

export function ErrorNotice({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-800">
      {children}
    </p>
  );
}

export function SuccessNotice({ children }: { children: ReactNode }) {
  return (
    <p role="status" className="rounded-md bg-green-50 px-3 py-2 text-sm text-green-800">
      {children}
    </p>
  );
}
