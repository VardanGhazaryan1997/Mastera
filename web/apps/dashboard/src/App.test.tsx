import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { createApiClient, type Me } from '@mastera/api-client';
import { createI18n } from '@mastera/i18n';
import { App } from './App';
import { createTenantStore } from './tenant/tenantStore';

const baseUrl = 'http://api.test';
const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const owner: Me = {
  userId: 'u1',
  email: 'nino@example.ge',
  displayName: 'Nino Kapanadze',
  memberships: [
    { tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Owner' },
    { tenantId: 't2', slug: 'nail-studio', name: 'Nail Studio', role: 'Master' },
  ],
};

function signedOut() {
  server.use(http.get(`${baseUrl}/api/auth/me`, () => new HttpResponse(null, { status: 401 })));
}

function signedIn(me: Me = owner) {
  server.use(http.get(`${baseUrl}/api/auth/me`, () => HttpResponse.json(me)));
}

function renderApp(language = 'en') {
  const tenantStore = createTenantStore();
  const api = createApiClient({ baseUrl, getTenant: tenantStore.get });
  render(<App i18n={createI18n(language)} api={api} tenantStore={tenantStore} />);
  return { tenantStore };
}

describe('Sign-in', () => {
  it.each([
    ['ka', 'შესვლა Mastera-ში'],
    ['ru', 'Вход в Mastera'],
    ['en', 'Sign in to Mastera'],
  ] as const)('shows the sign-in form in "%s" when not signed in', async (lng, title) => {
    signedOut();
    renderApp(lng);

    expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument();
  });

  it('checks the fields before calling the API', async () => {
    signedOut();
    let called = false;
    server.use(
      http.post(`${baseUrl}/api/auth/login`, () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp();

    await userEvent.click(await screen.findByRole('button', { name: 'Sign in' }));

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Fill in this field.');
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Fill in this field.');
    expect(called).toBe(false);
  });

  it('rejects an email without @', async () => {
    signedOut();
    renderApp();

    await userEvent.type(await screen.findByLabelText('Email'), 'nino');
    await userEvent.type(screen.getByLabelText('Password'), 'secret123');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter a valid email address.');
  });

  it('shows a clear message for a wrong password', async () => {
    signedOut();
    server.use(
      http.post(`${baseUrl}/api/auth/login`, () =>
        HttpResponse.json(
          { status: 401, code: 'auth.invalid_credentials' },
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    renderApp();

    await userEvent.type(await screen.findByLabelText('Email'), 'nino@example.ge');
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-pass1');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Wrong email or password.');
  });

  it('opens the dashboard after signing in', async () => {
    signedOut();
    server.use(
      http.post(`${baseUrl}/api/auth/login`, () => {
        signedIn();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp();

    await userEvent.type(await screen.findByLabelText('Email'), 'nino@example.ge');
    await userEvent.type(screen.getByLabelText('Password'), 'secret123');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Nino Kapanadze')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Calendar' })).toBeInTheDocument();
  });

  it('signs out back to the sign-in form', async () => {
    signedIn();
    server.use(
      http.post(`${baseUrl}/api/auth/logout`, () => {
        signedOut();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp();

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in to Mastera' })).toBeInTheDocument();
  });
});

describe('Salons and roles', () => {
  const menu = () => within(screen.getByRole('navigation', { name: 'Main menu' }));

  it('shows owners the salon management menu', async () => {
    signedIn();
    renderApp();

    await screen.findByText('Nino Kapanadze');

    expect(menu().getByRole('button', { name: 'Masters' })).toBeInTheDocument();
    expect(menu().getByRole('button', { name: 'Services' })).toBeInTheDocument();
    expect(menu().getByRole('button', { name: 'Settings' })).toBeInTheDocument();
  });

  it('hides salon management from masters', async () => {
    signedIn({ ...owner, memberships: [{ tenantId: 't2', slug: 'nail-studio', name: 'Nail Studio', role: 'Master' }] });
    renderApp();

    await screen.findByText('Nino Kapanadze');

    expect(menu().getByRole('button', { name: 'Calendar' })).toBeInTheDocument();
    expect(menu().queryByRole('button', { name: 'Masters' })).not.toBeInTheDocument();
    expect(menu().queryByRole('button', { name: 'Settings' })).not.toBeInTheDocument();
  });

  it('switches salon, changes the menu to the role there and remembers the choice', async () => {
    signedIn();
    const { tenantStore } = renderApp();

    await userEvent.selectOptions(await screen.findByLabelText('Salon'), 'nail-studio');

    await waitFor(() => expect(menu().queryByRole('button', { name: 'Masters' })).not.toBeInTheDocument());
    expect(screen.getByText('Master')).toBeInTheDocument();
    expect(tenantStore.get()).toBe('nail-studio');
  });

  it('loads the masters of the active salon with the salon header', async () => {
    signedIn();
    const headers: (string | null)[] = [];
    server.use(
      http.get(`${baseUrl}/api/members`, ({ request }) => {
        headers.push(request.headers.get('X-Mastera-Tenant'));
        return HttpResponse.json([
          { userId: 'u1', email: 'nino@example.ge', displayName: 'Nino Kapanadze', role: 'Owner' },
          { userId: 'u2', email: 'luka@example.ge', displayName: 'Luka Barber', role: 'Master' },
        ]);
      }),
    );
    renderApp();

    await userEvent.click(await within(await screen.findByRole('navigation', { name: 'Main menu' })).findByRole('button', { name: 'Masters' }));

    expect(await screen.findByText('Luka Barber')).toBeInTheDocument();
    expect(headers).toEqual(['barber-house']);
  });

  it('tells a user with no salons what is going on', async () => {
    signedIn({ ...owner, memberships: [] });
    renderApp();

    expect(await screen.findByText("You don't work in any salon yet.")).toBeInTheDocument();
  });
});
