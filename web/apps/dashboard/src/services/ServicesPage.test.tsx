import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { Service } from '@mastera/api-client';
import { baseUrl, openPage, problem, renderApp, server, signedIn, setupMockServer } from '../test/harness';

setupMockServer();

const haircut: Service = {
  id: 's1', categoryId: 'c1', name: "Men's haircut", description: null, durationMinutes: 45, price: 35.5, isArchived: false,
};

function catalog(services: Service[] = [haircut]) {
  server.use(
    http.get(`${baseUrl}/api/service-categories`, () => HttpResponse.json([{ id: 'c1', name: 'Haircuts', sortOrder: 0 }])),
    http.get(`${baseUrl}/api/services`, () => HttpResponse.json(services)),
  );
}

describe('Services page', () => {
  it('lists services with category, duration and price', async () => {
    signedIn();
    catalog();
    renderApp();

    await openPage('Services');

    const row = (await screen.findByText("Men's haircut")).closest('tr')!;
    expect(within(row).getByText('Haircuts')).toBeInTheDocument();
    expect(within(row).getByText('45 min')).toBeInTheDocument();
    expect(within(row).getByText('35.50')).toBeInTheDocument();
  });

  it('checks the form before sending', async () => {
    signedIn();
    catalog([]);
    let called = false;
    server.use(http.post(`${baseUrl}/api/services`, () => { called = true; return HttpResponse.json({}, { status: 201 }); }));
    renderApp();
    await openPage('Services');

    await userEvent.click(await screen.findByRole('button', { name: 'Add service' }));
    await userEvent.type(screen.getByLabelText('Name'), 'H');
    await userEvent.clear(screen.getByLabelText('Duration (minutes)'));
    await userEvent.type(screen.getByLabelText('Duration (minutes)'), '7');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(screen.getByLabelText('Name')).toHaveAccessibleDescription('Use 2–100 characters.');
    expect(screen.getByLabelText('Duration (minutes)')).toHaveAccessibleDescription('Use 5–480 minutes, in steps of 5.');
    expect(called).toBe(false);
  });

  it('adds a service in a category', async () => {
    signedIn();
    catalog([]);
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/services`, async ({ request }) => {
        body = await request.json();
        catalog([{ ...haircut, name: 'Beard trim', durationMinutes: 20, price: 15 }]);
        return HttpResponse.json({}, { status: 201 });
      }),
    );
    renderApp();
    await openPage('Services');

    await userEvent.click(await screen.findByRole('button', { name: 'Add service' }));
    await userEvent.type(screen.getByLabelText('Name'), 'Beard trim');
    await userEvent.selectOptions(screen.getByLabelText('Category'), 'c1');
    await userEvent.clear(screen.getByLabelText('Duration (minutes)'));
    await userEvent.type(screen.getByLabelText('Duration (minutes)'), '20');
    await userEvent.type(screen.getByLabelText('Price'), '15');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Beard trim')).toBeInTheDocument();
    expect(body).toEqual({ name: 'Beard trim', categoryId: 'c1', durationMinutes: 20, price: 15, description: null });
  });

  it('shows the API error when the name is taken', async () => {
    signedIn();
    catalog([]);
    server.use(http.post(`${baseUrl}/api/services`, () => problem(409, 'service.name.taken')));
    renderApp();
    await openPage('Services');

    await userEvent.click(await screen.findByRole('button', { name: 'Add service' }));
    await userEvent.type(screen.getByLabelText('Name'), 'Haircut');
    await userEvent.type(screen.getByLabelText('Price'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('A service with this name already exists.');
  });

  it('archives a service', async () => {
    signedIn();
    catalog();
    let archived = false;
    server.use(
      http.post(`${baseUrl}/api/services/s1/archive`, () => {
        archived = true;
        catalog([]);
        return HttpResponse.json({ ...haircut, isArchived: true });
      }),
    );
    renderApp();
    await openPage('Services');

    await userEvent.click(await screen.findByRole('button', { name: "Archive Men's haircut" }));

    await waitFor(() => expect(screen.queryByText("Men's haircut")).not.toBeInTheDocument());
    expect(archived).toBe(true);
  });

  it('adds a category', async () => {
    signedIn();
    catalog([]);
    let body: unknown;
    server.use(
      http.post(`${baseUrl}/api/service-categories`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ id: 'c2', name: 'Nails', sortOrder: 0 }, { status: 201 });
      }),
    );
    renderApp();
    await openPage('Services');

    await userEvent.type(await screen.findByLabelText('New category'), 'Nails');
    await userEvent.click(screen.getByRole('button', { name: 'Add category' }));

    await waitFor(() => expect(body).toEqual({ name: 'Nails', sortOrder: 0 }));
  });
});
