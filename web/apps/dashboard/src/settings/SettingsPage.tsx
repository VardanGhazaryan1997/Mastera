import { useQuery } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { ErrorNotice } from '../common/Notice';
import { InviteForm } from '../invite/InviteForm';

/** The salon's team (everyone with an account here) and inviting admins, receptionists and masters. */
export function SettingsPage({ api, salon }: { api: ApiClient; salon: string }) {
  const { t } = useTranslation();
  const members = useQuery({ queryKey: ['members', salon], queryFn: () => api.listMembers() });

  return (
    <section aria-labelledby="team-heading" className="flex flex-col gap-4">
      <h2 id="team-heading" className="text-lg font-semibold">
        {t('team.title')}
      </h2>
      {members.isPending ? (
        <p className="text-neutral-500">{t('common.loading')}</p>
      ) : members.isError ? (
        <ErrorNotice>{t('errors.generic')}</ErrorNotice>
      ) : members.data.length === 0 ? (
        <p className="text-neutral-600">{t('team.empty')}</p>
      ) : (
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
      )}
      <div className="rounded-lg border border-neutral-200 bg-white p-4">
        <InviteForm api={api} />
      </div>
    </section>
  );
}
