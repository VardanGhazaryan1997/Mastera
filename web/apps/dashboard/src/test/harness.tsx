import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { createApiClient, type Me } from '@mastera/api-client';
import { createI18n } from '@mastera/i18n';
import { App } from '../App';
import { createTenantStore } from '../tenant/tenantStore';

export const baseUrl = 'http://api.test';
export const server = setupServer();

export function setupMockServer() {
  beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
  afterEach(() => server.resetHandlers());
  afterAll(() => server.close());
}

export const owner: Me = {
  userId: 'u1',
  email: 'nino@example.ge',
  displayName: 'Nino Kapanadze',
  memberships: [
    { tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Owner' },
    { tenantId: 't2', slug: 'nail-studio', name: 'Nail Studio', role: 'Master' },
  ],
};

export function signedOut() {
  server.use(http.get(`${baseUrl}/api/auth/me`, () => new HttpResponse(null, { status: 401 })));
}

export function signedIn(me: Me = owner) {
  server.use(http.get(`${baseUrl}/api/auth/me`, () => HttpResponse.json(me)));
}

export function renderApp(options: { language?: string; path?: string } = {}) {
  window.history.replaceState(null, '', options.path ?? '/');
  const tenantStore = createTenantStore();
  const api = createApiClient({ baseUrl, getTenant: tenantStore.get });
  render(<App i18n={createI18n(options.language ?? 'en')} api={api} tenantStore={tenantStore} />);
  return { tenantStore };
}

export async function openPage(name: string) {
  const menu = within(await screen.findByRole('navigation', { name: 'Main menu' }));
  await userEvent.click(menu.getByRole('button', { name }));
}

export function problem(status: number, code: string, title = 'Problem') {
  return HttpResponse.json({ status, title, code }, { status, headers: { 'Content-Type': 'application/problem+json' } });
}
