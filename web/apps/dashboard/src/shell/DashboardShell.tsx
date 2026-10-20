import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiClient, Me } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, Select } from '@mastera/ui';
import { LanguageSwitcher } from '../LanguageSwitcher';
import { MastersPage } from '../masters/MastersPage';
import { MySchedulePage } from '../schedule/MySchedulePage';
import { ServicesPage } from '../services/ServicesPage';
import { SettingsPage } from '../settings/SettingsPage';
import type { TenantStore } from '../tenant/tenantStore';
import { pagesFor, type PageKey } from './navigation';

interface Props {
  api: ApiClient;
  me: Me;
  tenantStore: TenantStore;
}

export function DashboardShell({ api, me, tenantStore }: Props) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  // The active salon is written to the store synchronously, so requests made while rendering the
  // next page already carry the right X-Mastera-Tenant header.
  const [salon, setSalonState] = useState<string | null>(() => {
    const saved = tenantStore.get();
    const initial = me.memberships.some((m) => m.slug === saved) ? saved : (me.memberships[0]?.slug ?? null);
    tenantStore.set(initial);
    return initial;
  });
  const setSalon = (slug: string) => {
    tenantStore.set(slug);
    setSalonState(slug);
  };

  const [page, setPage] = useState<PageKey>('calendar');
  const membership = me.memberships.find((m) => m.slug === salon);
  const menu = pagesFor(membership?.role);
  const activePage = menu.some((p) => p.key === page) ? page : 'calendar';

  const logout = useMutation({
    mutationFn: () => api.logout(),
    onSuccess: () => {
      for (const key of ['members', 'masters', 'services', 'categories', 'schedule', 'my-master']) queryClient.removeQueries({ queryKey: [key] });
      return queryClient.invalidateQueries({ queryKey: ['me'] });
    },
  });

  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-neutral-200 bg-white px-4 py-3 sm:px-6">
        <div className="flex flex-wrap items-center gap-4">
          <span className="text-lg font-semibold tracking-tight">{t('common.appName')}</span>
          {me.memberships.length > 1 && salon && (
            <Select
              label={t('salon.label')}
              value={salon}
              options={me.memberships.map((m) => ({ value: m.slug, label: m.name }))}
              onChange={setSalon}
            />
          )}
          {me.memberships.length === 1 && membership && <span className="font-medium">{membership.name}</span>}
          {membership && (
            <span className="rounded-full bg-neutral-100 px-2 py-0.5 text-xs text-neutral-700">{t(`roles.${membership.role}`)}</span>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-4">
          <span className="text-sm text-neutral-700">{me.displayName}</span>
          <LanguageSwitcher />
          <Button variant="secondary" onClick={() => logout.mutate()} disabled={logout.isPending}>
            {t('auth.signOut')}
          </Button>
        </div>
      </header>

      {!membership ? (
        <main className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
          <p className="text-neutral-700">{t('salon.none')}</p>
        </main>
      ) : (
        <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-6 sm:flex-row sm:px-6">
          <nav aria-label={t('nav.label')} className="sm:w-48">
            <ul className="flex flex-wrap gap-1 sm:flex-col">
              {menu.map((p) => (
                <li key={p.key}>
                  <button
                    type="button"
                    aria-current={p.key === activePage ? 'page' : undefined}
                    onClick={() => setPage(p.key)}
                    className={`w-full rounded-md px-3 py-2 text-left text-sm ${
                      p.key === activePage ? 'bg-neutral-900 text-white' : 'hover:bg-neutral-200'
                    }`}
                  >
                    {t(`nav.${p.key}`)}
                  </button>
                </li>
              ))}
            </ul>
          </nav>
          <main className="min-w-0 flex-1">
            <h1 className="mb-4 text-2xl font-semibold">{t(`nav.${activePage}`)}</h1>
            {activePage === 'masters' ? (
              <MastersPage api={api} salon={membership.slug} />
            ) : activePage === 'schedule' ? (
              <MySchedulePage api={api} salon={membership.slug} />
            ) : activePage === 'services' ? (
              <ServicesPage api={api} salon={membership.slug} />
            ) : activePage === 'settings' ? (
              <SettingsPage api={api} salon={membership.slug} />
            ) : (
              <p className="text-neutral-600">{t('pages.comingSoon')}</p>
            )}
          </main>
        </div>
      )}
    </div>
  );
}
