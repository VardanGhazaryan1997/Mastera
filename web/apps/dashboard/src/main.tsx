import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createApiClient } from '@mastera/api-client';
import { createI18n } from '@mastera/i18n';
import { App } from './App';
import { createSignalRRealtime } from './notifications/realtime';
import { createTenantStore } from './tenant/tenantStore';
import './styles.css';

function readLanguage(): string | undefined {
  try {
    return localStorage.getItem('mastera.lang') ?? undefined;
  } catch {
    return undefined;
  }
}

const i18n = createI18n(readLanguage());
i18n.on('languageChanged', (lng) => {
  try {
    localStorage.setItem('mastera.lang', lng);
  } catch {
    // Storage blocked: the language just isn't remembered.
  }
});

const tenantStore = createTenantStore(window.localStorage);
// Same origin: Vite proxies /api to the API in development; the API serves the app in production.
const api = createApiClient({ baseUrl: '', getTenant: tenantStore.get });

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App i18n={i18n} api={api} tenantStore={tenantStore} realtime={createSignalRRealtime()} />
  </StrictMode>,
);
