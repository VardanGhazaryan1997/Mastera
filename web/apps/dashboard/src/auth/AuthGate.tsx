import { useQuery } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import type { Realtime } from '../notifications/realtime';
import { DashboardShell } from '../shell/DashboardShell';
import type { TenantStore } from '../tenant/tenantStore';
import { LoginPage } from './LoginPage';

/** Shows the sign-in form until the API says who the user is, then the dashboard. */
export function AuthGate({ api, tenantStore, realtime }: { api: ApiClient; tenantStore: TenantStore; realtime: Realtime }) {
  const { t } = useTranslation();
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.me() });

  if (me.isPending) return <p className="p-6 text-neutral-500">{t('common.loading')}</p>;
  if (!me.data) return <LoginPage api={api} />;
  return <DashboardShell api={api} me={me.data} tenantStore={tenantStore} realtime={realtime} />;
}
