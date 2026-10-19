import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { createI18n } from '@mastera/i18n';
import { App } from './App';

describe('Dashboard app shell', () => {
  it.each([
    ['ka', 'სალონის პანელი'],
    ['ru', 'Панель салона'],
    ['en', 'Salon dashboard'],
  ] as const)('renders in "%s"', (lng, title) => {
    render(<App i18n={createI18n(lng)} />);

    expect(screen.getByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getByText('Mastera')).toBeInTheDocument();
  });

  it('switches language from the header', async () => {
    render(<App i18n={createI18n('ka')} />);

    await userEvent.selectOptions(screen.getByLabelText('ენა'), 'en');

    expect(await screen.findByRole('heading', { level: 1, name: 'Salon dashboard' })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('en');
  });
});
