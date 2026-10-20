import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { createApiClient } from './index';

const baseUrl = 'http://api.test';
const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const api = () => createApiClient({ baseUrl, getTenant: () => 'barber-house' });

describe('catalog and masters', () => {
  it('creates a service with the active salon header', async () => {
    let body: unknown;
    let tenant: string | null = null;
    server.use(
      http.post(`${baseUrl}/api/services`, async ({ request }) => {
        body = await request.json();
        tenant = request.headers.get('X-Mastera-Tenant');
        return HttpResponse.json({ id: 's1' }, { status: 201 });
      }),
    );

    await api().createService({ name: 'Haircut', durationMinutes: 45, price: 35 });

    expect(body).toEqual({ name: 'Haircut', durationMinutes: 45, price: 35 });
    expect(tenant).toBe('barber-house');
  });

  it('asks for archived services only when wanted', async () => {
    const urls: string[] = [];
    server.use(
      http.get(`${baseUrl}/api/services`, ({ request }) => {
        urls.push(new URL(request.url).search);
        return HttpResponse.json([]);
      }),
    );

    await api().listServices();
    await api().listServices(true);

    expect(urls).toEqual(['', '?includeArchived=true']);
  });

  it('sends master services with overrides', async () => {
    let body: unknown;
    server.use(
      http.put(`${baseUrl}/api/masters/m1/services`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ id: 'm1', services: [] });
      }),
    );

    await api().setMasterServices('m1', [{ serviceId: 's1', price: 50, durationMinutes: null }]);

    expect(body).toEqual({ services: [{ serviceId: 's1', price: 50, durationMinutes: null }] });
  });
});

describe('invitations', () => {
  it('reads and accepts an invitation without the salon header', async () => {
    const tenants: (string | null)[] = [];
    server.use(
      http.get(`${baseUrl}/api/invitations/abc`, ({ request }) => {
        tenants.push(request.headers.get('X-Mastera-Tenant'));
        return HttpResponse.json({ salonName: 'Barber House' });
      }),
      http.post(`${baseUrl}/api/invitations/accept`, ({ request }) => {
        tenants.push(request.headers.get('X-Mastera-Tenant'));
        return HttpResponse.json({ slug: 'barber-house' });
      }),
    );

    await api().getInvitation('abc');
    await api().acceptInvitation({ token: 'abc', displayName: 'Luka', password: 'secret123' });

    expect(tenants).toEqual([null, null]);
  });
});

describe('schedules and availability', () => {
  it('saves a weekly pattern', async () => {
    let body: unknown;
    server.use(
      http.put(`${baseUrl}/api/masters/m1/schedule/pattern`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ masterId: 'm1', pattern: null, exceptions: [] });
      }),
    );

    await api().setPattern('m1', { kind: 'weekly', days: { Monday: { start: '10:00', end: '19:00' } } });

    expect(body).toEqual({ pattern: { kind: 'weekly', days: { Monday: { start: '10:00', end: '19:00' } } } });
  });

  it('asks availability for a date and, optionally, a master', async () => {
    const searches: string[] = [];
    server.use(
      http.get(`${baseUrl}/api/availability`, ({ request }) => {
        searches.push(new URL(request.url).search);
        return HttpResponse.json({ slots: [] });
      }),
    );

    await api().getAvailability('s1', '2026-11-02');
    await api().getAvailability('s1', '2026-11-02', 'm1');

    expect(searches).toEqual(['?serviceId=s1&date=2026-11-02', '?serviceId=s1&date=2026-11-02&masterId=m1']);
  });
});
