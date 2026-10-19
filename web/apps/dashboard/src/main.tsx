import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createI18n } from '@mastera/i18n';
import { App } from './App';
import './styles.css';

const queryClient = new QueryClient();
const i18n = createI18n(localStorage.getItem('mastera.lang') ?? undefined);
i18n.on('languageChanged', (lng) => localStorage.setItem('mastera.lang', lng));

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App i18n={i18n} />
    </QueryClientProvider>
  </StrictMode>,
);
