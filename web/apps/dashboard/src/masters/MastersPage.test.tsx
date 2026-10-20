import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Master } from '@mastera/api-client';
import { baseUrl, openPage, renderApp, server, signedIn, setupMockServer } from '../test/harness';

setupMockServer();

const luka: Master = { id: 'm1', displayName: 'Luka', bio: 'Fades', isActive: true, userId: null, services: [] };

function data(masters: Master[] = [luka]) {
  server.use(
    http.get(`${baseUrl}/api/masters`, () => HttpResponse.json(masters)),
    http.get(`${baseUrl}/api/service-categories`, () => HttpResponse.json([])),
    http.get(`${baseUrl}/api/masters/:id/schedule`, ({ params }) => HttpResponse.json({ masterId: params.id, pattern: null, exceptions: [] })),
    http.get(`${baseUrl}/api/services`, () =>
      HttpResponse.json([
        { id: 's1', categoryId: null, name: 'Haircut', description: null, durationMinutes: 45, price: 35, isArchived: false },
        { id: 's2', categoryId: null, name: 'Beard trim', description: null, durationMinutes: 20, price: 15, isArchived: false },
      ]),
    ),
  );
}

describe('Masters page', () => {
  it('lists masters', async () => {
    signedIn();
    data();
    renderApp();

    await openPage('Masters');

    expect(await screen.findByRole('button', { name: /Luka/ })).toBeInTheDocument();
  });

  it('adds a master', async () => {
    signedIn();
    data([]);
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/masters`, async ({ request }) => {
        body = await request.json();
        data([{ ...luka, displayName: 'Ana' }]);
        return HttpResponse.json({ ...luka, displayName: 'Ana' }, { status: 201 });
      }),
    );
    renderApp();
    await openPage('Masters');

    await userEvent.type(await screen.findByLabelText('New master'), 'Ana');
    await userEvent.click(screen.getByRole('button', { name: 'Add master' }));

    expect(await screen.findByRole('button', { name: /Ana/ })).toBeInTheDocument();
    expect(body).toEqual({ displayName: 'Ana' });
  });

  it('assigns services with an own price', async () => {
    signedIn();
    data();
    let body: unknown;
    server.use(
      http.put(`${baseUrl}/api/masters/m1/services`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(luka);
      }),
    );
    renderApp();
    await openPage('Masters');
    await userEvent.click(await screen.findByRole('button', { name: /Luka/ }));

    const services = await screen.findByRole('group', { name: 'Services' });
    await userEvent.click(within(services).getByLabelText('Haircut'));
    await userEvent.type(within(services).getByLabelText('Own price for Haircut'), '50');
    await userEvent.click(within(services).getByLabelText('Beard trim'));
    await userEvent.click(screen.getByRole('button', { name: 'Save services' }));

    await waitFor(() =>
      expect(body).toEqual({
        services: [
          { serviceId: 's1', price: 50, durationMinutes: null },
          { serviceId: 's2', price: null, durationMinutes: null },
        ],
      }),
    );
  });

  it('invites the master by email', async () => {
    signedIn();
    data();
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/invitations`, async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 202 });
      }),
    );
    renderApp();
    await openPage('Masters');
    await userEvent.click(await screen.findByRole('button', { name: /Luka/ }));

    await userEvent.type(await screen.findByLabelText('Invite by email'), 'luka@example.ge');
    await userEvent.click(screen.getByRole('button', { name: 'Send invitation' }));

    expect(await screen.findByText('Invitation sent to luka@example.ge.')).toBeInTheDocument();
    expect(body).toEqual({ email: 'luka@example.ge', role: 'Master', masterId: 'm1' });
  });
});
