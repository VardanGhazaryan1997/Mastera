import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { baseUrl, openPage, renderApp, server, signedIn, setupMockServer } from '../test/harness';

setupMockServer();

describe('Settings: team', () => {
  it('lists the team of the active salon', async () => {
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

    await openPage('Settings');

    expect(await screen.findByText('Luka Barber')).toBeInTheDocument();
    expect(headers).toEqual(['barber-house']);
  });

  it('invites a receptionist', async () => {
    signedIn();
    let body: unknown;
    server.use(
      http.get(`${baseUrl}/api/members`, () => HttpResponse.json([])),
      http.post(`${baseUrl}/api/invitations`, async ({ request }) => {
        body = await request.json();
        return new HttpResponse(null, { status: 202 });
      }),
    );
    renderApp();
    await openPage('Settings');

    await userEvent.type(await screen.findByLabelText('Invite by email'), 'desk@example.ge');
    await userEvent.selectOptions(screen.getByLabelText('Role'), 'Receptionist');
    await userEvent.click(screen.getByRole('button', { name: 'Send invitation' }));

    await waitFor(() => expect(body).toEqual({ email: 'desk@example.ge', role: 'Receptionist', masterId: null }));
  });
});
