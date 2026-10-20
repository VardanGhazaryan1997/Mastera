import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Master, Schedule } from '@mastera/api-client';
import { baseUrl, openPage, owner, renderApp, server, setupMockServer, signedIn } from '../test/harness';

setupMockServer();

const luka: Master = { id: 'm1', displayName: 'Luka', bio: null, isActive: true, userId: 'u9', services: [] };

function data(schedule: Schedule = { masterId: 'm1', pattern: null, exceptions: [] }) {
  server.use(
    http.get(`${baseUrl}/api/masters`, () => HttpResponse.json([luka])),
    http.get(`${baseUrl}/api/masters/me`, () => HttpResponse.json(luka)),
    http.get(`${baseUrl}/api/service-categories`, () => HttpResponse.json([])),
    http.get(`${baseUrl}/api/services`, () => HttpResponse.json([])),
    http.get(`${baseUrl}/api/masters/m1/schedule`, () => HttpResponse.json(schedule)),
  );
}

const setValue = (element: HTMLElement, value: string) => fireEvent.change(element, { target: { value } });

async function openLuka() {
  await openPage('Masters');
  await userEvent.click(await screen.findByRole('button', { name: /Luka/ }));
  return screen.findByRole('region', { name: 'Schedule' });
}

describe('Schedule editor (owner)', () => {
  it('says when regular hours are not set', async () => {
    signedIn();
    data();
    renderApp();

    const schedule = await openLuka();

    expect(await within(schedule).findByText("Regular hours aren't set yet, so nobody can book this master.")).toBeInTheDocument();
  });

  it('saves weekly hours with a break', async () => {
    signedIn();
    data();
    let body: unknown;
    server.use(
      http.put(`${baseUrl}/api/masters/m1/schedule/pattern`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ masterId: 'm1', pattern: null, exceptions: [] });
      }),
    );
    renderApp();
    const schedule = await openLuka();

    await userEvent.click(await within(schedule).findByLabelText('Monday'));
    setValue(within(schedule).getByLabelText('Monday from'), '10:00');
    setValue(within(schedule).getByLabelText('Monday to'), '19:00');
    setValue(within(schedule).getByLabelText('Monday break from'), '13:00');
    setValue(within(schedule).getByLabelText('Monday break to'), '14:00');
    await userEvent.click(within(schedule).getByLabelText('Saturday'));
    await userEvent.click(within(schedule).getByRole('button', { name: 'Save regular hours' }));

    await waitFor(() =>
      expect(body).toEqual({
        pattern: {
          kind: 'weekly',
          days: {
            Monday: { start: '10:00', end: '19:00', breaks: [{ start: '13:00', end: '14:00' }] },
            Saturday: { start: '10:00', end: '19:00', breaks: [] },
          },
        },
      }),
    );
  });

  it('saves shifts as days on and days off', async () => {
    signedIn();
    data();
    let body: unknown;
    server.use(
      http.put(`${baseUrl}/api/masters/m1/schedule/pattern`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ masterId: 'm1', pattern: null, exceptions: [] });
      }),
    );
    renderApp();
    const schedule = await openLuka();

    await userEvent.click(await within(schedule).findByLabelText('Shifts (days on / days off)'));
    setValue(within(schedule).getByLabelText('First working day'), '2026-11-01');
    setValue(within(schedule).getByLabelText('Days on'), '2');
    setValue(within(schedule).getByLabelText('Days off'), '2');
    setValue(within(schedule).getByLabelText('Shift from'), '10:00');
    setValue(within(schedule).getByLabelText('Shift to'), '20:00');
    await userEvent.click(within(schedule).getByRole('button', { name: 'Save regular hours' }));

    const shift = { start: '10:00', end: '20:00', breaks: [] };
    await waitFor(() =>
      expect(body).toEqual({ pattern: { kind: 'rotating', anchor: '2026-11-01', cycle: [shift, shift, null, null] } }),
    );
  });

  it('shows saved weekly hours', async () => {
    signedIn();
    data({ masterId: 'm1', pattern: { kind: 'weekly', days: { Tuesday: { start: '11:00', end: '18:00', breaks: [] } } }, exceptions: [] });
    renderApp();
    const schedule = await openLuka();

    expect(await within(schedule).findByLabelText('Tuesday')).toBeChecked();
    expect(within(schedule).getByLabelText('Tuesday from')).toHaveValue('11:00');
    expect(within(schedule).getByLabelText('Monday')).not.toBeChecked();
  });

  it('adds and removes a day off', async () => {
    signedIn();
    data({
      masterId: 'm1',
      pattern: null,
      exceptions: [{ id: 'e1', from: '2026-11-10', to: '2026-11-14', hours: null, note: 'Vacation' }],
    });
    let added: unknown;
    let removed = false;
    server.use(
      http.post(`${baseUrl}/api/masters/m1/schedule/exceptions`, async ({ request }) => {
        added = await request.json();
        return HttpResponse.json({ id: 'e2' }, { status: 201 });
      }),
      http.delete(`${baseUrl}/api/masters/m1/schedule/exceptions/e1`, () => {
        removed = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp();
    const schedule = await openLuka();

    expect(await within(schedule).findByText(/Vacation/)).toBeInTheDocument();
    setValue(within(schedule).getByLabelText('First day'), '2026-11-20');
    await userEvent.type(within(schedule).getByLabelText('Note'), 'Doctor');
    await userEvent.click(within(schedule).getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(added).toEqual({ from: '2026-11-20', to: null, hours: null, note: 'Doctor' }));

    await userEvent.click(within(schedule).getByRole('button', { name: 'Remove 2026-11-10 – 2026-11-14' }));
    await waitFor(() => expect(removed).toBe(true));
  });

  it('explains overlapping days off', async () => {
    signedIn();
    data();
    server.use(
      http.post(`${baseUrl}/api/masters/m1/schedule/exceptions`, () =>
        HttpResponse.json({ status: 400, code: 'schedule.exception.overlap' }, { status: 400, headers: { 'Content-Type': 'application/problem+json' } }),
      ),
    );
    renderApp();
    const schedule = await openLuka();

    setValue(await within(schedule).findByLabelText('First day'), '2026-11-20');
    await userEvent.click(within(schedule).getByRole('button', { name: 'Add' }));

    expect(await within(schedule).findByRole('alert')).toHaveTextContent('These dates overlap another day off or change.');
  });
});

describe('My schedule (master)', () => {
  it('lets a master manage their own days off but not their regular hours', async () => {
    signedIn({ ...owner, memberships: [{ tenantId: 't1', slug: 'barber-house', name: 'Barber House', role: 'Master' }] });
    data({ masterId: 'm1', pattern: { kind: 'weekly', days: { Monday: { start: '10:00', end: '19:00', breaks: [] } } }, exceptions: [] });
    renderApp();

    await openPage('My schedule');
    const schedule = await screen.findByRole('region', { name: 'Schedule' });

    expect(await within(schedule).findByLabelText('First day')).toBeInTheDocument();
    expect(within(schedule).queryByRole('button', { name: 'Save regular hours' })).not.toBeInTheDocument();
    expect(within(schedule).getByText('Monday: 10:00–19:00')).toBeInTheDocument();
  });
});
