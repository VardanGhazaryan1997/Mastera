import { useQuery } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';

export function MastersPage({ api, salon }: { api: ApiClient; salon: string }) {
  const { t } = useTranslation();
  const members = useQuery({ queryKey: ['members', salon], queryFn: () => api.listMembers() });

  if (members.isPending) return <p className="text-neutral-500">{t('common.loading')}</p>;
  if (members.isError) return <p role="alert">{t('auth.genericError')}</p>;
  if (members.data.length === 0) return <p className="text-neutral-600">{t('masters.empty')}</p>;

  return (
    <ul className="divide-y divide-neutral-200 rounded-lg border border-neutral-200 bg-white">
      {members.data.map((m) => (
        <li key={m.userId} className="flex flex-wrap items-center justify-between gap-2 px-4 py-3">
          <div className="min-w-0">
            <p className="font-medium">{m.displayName}</p>
            <p className="truncate text-sm text-neutral-600">{m.email}</p>
          </div>
          <span className="rounded-full bg-neutral-100 px-2 py-0.5 text-xs text-neutral-700">{t(`roles.${m.role}`)}</span>
        </li>
      ))}
    </ul>
  );
}
