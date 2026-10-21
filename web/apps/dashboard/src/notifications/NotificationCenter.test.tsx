import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { LiveNotification } from '@mastera/api-client';
import { renderApp, signedIn, setupMockServer, owner } from '../test/harness';
import { createFakeRealtime } from './realtime';

setupMockServer();

const booked: LiveNotification = {
  kind: 'booking.created',
  bookingId: 'b1',
  masterId: 'm1',
  masterName: 'Luka',
  serviceName: 'Haircut',
  clientName: 'Nino',
  start: '2026-11-02T08:00:00Z',
  occurredAt: '2026-10-21T10:00:00Z',
};

describe('Notification bell', () => {
  it('listens to the active salon and shows a toast and a badge for a new booking', async () => {
    signedIn();
    const fake = createFakeRealtime();
    renderApp({ realtime: fake.realtime });

    const bell = await screen.findByRole('button', { name: 'Notifications' });
    expect(fake.subscribedSalons()).toEqual(['barber-house']);
    expect(screen.queryByTestId('bell-badge')).not.toBeInTheDocument();

    act(() => fake.emit('barber-house', booked));

    expect(await screen.findByRole('status')).toHaveTextContent('New booking: Nino — Haircut with Luka');
    expect(screen.getByTestId('bell-badge')).toHaveTextContent('1');
    expect(bell).toHaveAccessibleName('Notifications, 1 new');
  });

  it('lists notifications and marks them read when the list is closed', async () => {
    signedIn();
    const fake = createFakeRealtime();
    renderApp({ realtime: fake.realtime });
    const bell = await screen.findByRole('button', { name: 'Notifications' });
    act(() => {
      fake.emit('barber-house', booked);
      fake.emit('barber-house', { ...booked, bookingId: 'b2', kind: 'booking.cancelled', clientName: 'Giorgi' });
    });

    await userEvent.click(bell);
    const list = within(screen.getByRole('dialog', { name: 'Notifications' }));
    expect(list.getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      expect.stringContaining('Cancelled: Giorgi'),
      expect.stringContaining('New booking: Nino'),
    ]);

    await userEvent.click(bell);
    expect(screen.queryByTestId('bell-badge')).not.toBeInTheDocument();
  });

  it('toasts disappear after a few seconds', async () => {
    signedIn();
    const fake = createFakeRealtime();
    renderApp({ realtime: fake.realtime });
    await screen.findByRole('button', { name: 'Notifications' });

    vi.useFakeTimers();
    try {
      act(() => fake.emit('barber-house', booked));
      expect(screen.getByRole('status')).toBeInTheDocument();
      act(() => vi.advanceTimersByTime(6500));
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
      expect(screen.getByTestId('bell-badge')).toHaveTextContent('1');
    } finally {
      vi.useRealTimers();
    }
  });

  it('switches to the other salon when the user does', async () => {
    signedIn(owner);
    const fake = createFakeRealtime();
    renderApp({ realtime: fake.realtime });
    await screen.findByRole('button', { name: 'Notifications' });

    await userEvent.selectOptions(screen.getByLabelText('Salon'), 'nail-studio');

    expect(fake.subscribedSalons()).toEqual(['nail-studio']);
  });
});
