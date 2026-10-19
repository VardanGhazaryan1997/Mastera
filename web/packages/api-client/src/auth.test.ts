import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { ApiError, createApiClient, type Me } from './index';

const baseUrl = 'http://api.test';
const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const me: Me = {
  userId: 'u1',
  email: 'nino@example.ge',
  displayName: 'Nino',
  memberships: [{ tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Owner' }],
};

describe('auth', () => {
  it('signs in with email and password as JSON', async () => {
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/auth/login`, async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );

    await createApiClient({ baseUrl }).login('nino@example.ge', 'secret123');

    expect(body).toEqual({ email: 'nino@example.ge', password: 'secret123' });
  });

  it('reports a wrong password as an ApiError with the problem code', async () => {
    server.use(
      http.post(`${baseUrl}/api/auth/login`, () =>
        HttpResponse.json(
          { status: 401, title: 'Wrong email or password.', code: 'auth.invalid_credentials' },
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await createApiClient({ baseUrl }).login('a@b.ge', 'x').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).code).toBe('auth.invalid_credentials');
  });

  it('returns the signed-in user', async () => {
    server.use(http.get(`${baseUrl}/api/auth/me`, () => HttpResponse.json(me)));

    await expect(createApiClient({ baseUrl }).me()).resolves.toEqual(me);
  });

  it('returns null from me() when not signed in', async () => {
    server.use(http.get(`${baseUrl}/api/auth/me`, () => new HttpResponse(null, { status: 401 })));

    await expect(createApiClient({ baseUrl }).me()).resolves.toBeNull();
  });

  it('signs out', async () => {
    let called = false;
    server.use(
      http.post(`${baseUrl}/api/auth/logout`, () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    await createApiClient({ baseUrl }).logout();

    expect(called).toBe(true);
  });
});

describe('salon header', () => {
  it('sends the active salon in X-Mastera-Tenant', async () => {
    let header: string | null = null;
    server.use(
      http.get(`${baseUrl}/api/members`, ({ request }) => {
        header = request.headers.get('X-Mastera-Tenant');
        return HttpResponse.json([]);
      }),
    );

    await createApiClient({ baseUrl, getTenant: () => 'barber-house' }).listMembers();

    expect(header).toBe('barber-house');
  });

  it('sends no salon header when none is active', async () => {
    let header: string | null = 'unset';
    server.use(
      http.get(`${baseUrl}/api/members`, ({ request }) => {
        header = request.headers.get('X-Mastera-Tenant');
        return HttpResponse.json([]);
      }),
    );

    await createApiClient({ baseUrl, getTenant: () => null }).listMembers();

    expect(header).toBeNull();
  });
});
