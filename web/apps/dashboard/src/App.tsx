import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { I18nextProvider, type i18n } from '@mastera/i18n';
import { AuthGate } from './auth/AuthGate';
import { InvitePage } from './invite/InvitePage';
import { noRealtime, type Realtime } from './notifications/realtime';
import type { TenantStore } from './tenant/tenantStore';

export interface AppProps {
  i18n: i18n;
  api: ApiClient;
  tenantStore: TenantStore;
  /** Live notifications (SignalR in the app, a fake in tests). */
  realtime?: Realtime;
}

function invitationTokenFromUrl(): string | null {
  return window.location.pathname === '/invite' ? new URLSearchParams(window.location.search).get('token') : null;
}

export function App({ i18n, api, tenantStore, realtime = noRealtime }: AppProps) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } }),
  );
  const [inviteToken, setInviteToken] = useState(invitationTokenFromUrl);

  const leaveInvitation = () => {
    window.history.replaceState(null, '', '/');
    setInviteToken(null);
  };

  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        {inviteToken ? (
          <InvitePage api={api} token={inviteToken} onDone={leaveInvitation} />
        ) : (
          <AuthGate api={api} tenantStore={tenantStore} realtime={realtime} />
        )}
      </QueryClientProvider>
    </I18nextProvider>
  );
}
