import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { InvitationDetails } from '@mastera/api-client';
import { baseUrl, owner, renderApp, server, signedIn, signedOut, setupMockServer } from '../test/harness';

setupMockServer();

const details: InvitationDetails = {
  salonName: 'Barber House', salonSlug: 'barber-house', email: 'luka@example.ge', role: 'Master',
  expired: false, accepted: false, hasAccount: false,
};

function invitation(d: InvitationDetails = details) {
  server.use(http.get(`${baseUrl}/api/invitations/tok123`, () => HttpResponse.json(d)));
}

describe('Accepting an invitation', () => {
  it('creates an account and joins the salon', async () => {
    signedOut();
    invitation();
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/invitations/accept`, async ({ request }) => {
        body = await request.json();
        signedIn({ ...owner, memberships: [{ tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Master' }] });
        return HttpResponse.json({ slug: 'barber-house', accountCreated: true });
      }),
    );
    renderApp({ path: '/invite?token=tok123' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Join Barber House' })).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Your name'), 'Luka Barber');
    await userEvent.type(screen.getByLabelText('Password'), 'secret123');
    await userEvent.click(screen.getByRole('button', { name: 'Accept invitation' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Calendar' })).toBeInTheDocument();
    expect(body).toEqual({ token: 'tok123', displayName: 'Luka Barber', password: 'secret123' });
    expect(window.location.pathname).toBe('/');
  });

  it('asks people with an account to sign in first', async () => {
    signedOut();
    invitation({ ...details, hasAccount: true });
    renderApp({ path: '/invite?token=tok123' });

    expect(await screen.findByText('Sign in as luka@example.ge to accept.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Sign in to Mastera' })).toBeInTheDocument();
  });

  it('lets a signed-in user accept with one click', async () => {
    signedIn({ ...owner, email: 'luka@example.ge' });
    invitation({ ...details, hasAccount: true });
    let called = false;
    server.use(http.post(`${baseUrl}/api/invitations/accept`, () => { called = true; return HttpResponse.json({ slug: 'barber-house', accountCreated: false }); }));
    renderApp({ path: '/invite?token=tok123' });

    await userEvent.click(await screen.findByRole('button', { name: 'Accept invitation' }));

    await waitFor(() => expect(called).toBe(true));
  });

  it('explains an expired invitation', async () => {
    signedOut();
    invitation({ ...details, expired: true });
    renderApp({ path: '/invite?token=tok123' });

    expect(await screen.findByText('This invitation has expired. Ask the salon for a new one.')).toBeInTheDocument();
  });
});
