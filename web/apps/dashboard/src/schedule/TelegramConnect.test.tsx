import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Me } from '@mastera/api-client';
import { baseUrl, openPage, renderApp, server, setupMockServer, signedIn } from '../test/harness';

setupMockServer();

const master: Me = {
  userId: 'u9',
  email: 'luka@example.ge',
  displayName: 'Luka',
  memberships: [{ tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Master' }],
};

describe('Connect Telegram (master)', () => {
  it('gets a one-time link to the bot', async () => {
    signedIn(master);
    server.use(
      http.get(`${baseUrl}/api/masters/me`, () => HttpResponse.json({ id: 'm1', displayName: 'Luka', bio: null, isActive: true, userId: 'u9', services: [] })),
      http.get(`${baseUrl}/api/masters/m1/schedule`, () => HttpResponse.json({ masterId: 'm1', pattern: null, exceptions: [] })),
      http.post(`${baseUrl}/api/telegram/link`, () =>
        HttpResponse.json({ url: 'https://t.me/MasteraBot?start=abc123', expiresAt: '2026-10-21T10:30:00Z', linked: false })),
    );
    renderApp();
    await openPage('My schedule');

    await userEvent.click(await screen.findByRole('button', { name: 'Connect Telegram' }));

    const link = await screen.findByRole('link', { name: 'Open Telegram and press Start' });
    expect(link).toHaveAttribute('href', 'https://t.me/MasteraBot?start=abc123');
    expect(link).toHaveAttribute('target', '_blank');
  });
});
