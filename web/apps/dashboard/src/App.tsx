import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { I18nextProvider, type i18n } from '@mastera/i18n';
import { AuthGate } from './auth/AuthGate';
import type { TenantStore } from './tenant/tenantStore';

export interface AppProps {
  i18n: i18n;
  api: ApiClient;
  tenantStore: TenantStore;
}

export function App({ i18n, api, tenantStore }: AppProps) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } } }),
  );

  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AuthGate api={api} tenantStore={tenantStore} />
      </QueryClientProvider>
    </I18nextProvider>
  );
}
