import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { baseUrl, openPage, renderApp, server, signedIn, setupMockServer } from '../test/harness';

setupMockServer();

function salonSettings() {
  const saved: unknown[] = [];
  server.use(
    http.get(`${baseUrl}/api/members`, () => HttpResponse.json([])),
    http.get(`${baseUrl}/api/settings/booking`, () => HttpResponse.json({ hideClientPhoneFromMasters: false, cancellationWindowHours: 2 })),
    http.put(`${baseUrl}/api/settings/booking`, async ({ request }) => {
      const body = await request.json();
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
  return saved;
}

describe('Settings: booking rules', () => {
  it('shows the current rules and saves changes', async () => {
    signedIn();
    const saved = salonSettings();
    renderApp();
    await openPage('Settings');

    const window = await screen.findByLabelText('Clients can cancel or move online until (hours before)');
    expect(window).toHaveValue('2');
    await userEvent.clear(window);
    await userEvent.type(window, '4');
    await userEvent.click(screen.getByLabelText("Hide clients' phone numbers from masters"));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(saved).toEqual([{ hideClientPhoneFromMasters: true, cancellationWindowHours: 4 }]));
    expect(await screen.findByText('Saved.')).toBeInTheDocument();
  });

  it('rejects a window that is not a whole number of hours', async () => {
    signedIn();
    const saved = salonSettings();
    renderApp();
    await openPage('Settings');

    const window = await screen.findByLabelText('Clients can cancel or move online until (hours before)');
    await userEvent.clear(window);
    await userEvent.type(window, '1.5');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Enter a whole number of hours from 0 to 168.')).toBeInTheDocument();
    expect(saved).toEqual([]);
  });
});
