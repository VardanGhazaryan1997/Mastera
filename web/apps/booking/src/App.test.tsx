import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { createI18n } from '@mastera/i18n';
import { App } from './App';

describe('Booking app shell', () => {
  it.each([
    ['ka', 'ვიზიტის დაჯავშნა', 'დროის არჩევა'],
    ['ru', 'Записаться онлайн', 'Выбрать время'],
    ['en', 'Book an appointment', 'Choose a time'],
  ] as const)('renders in "%s"', (lng, title, cta) => {
    render(<App i18n={createI18n(lng)} />);

    expect(screen.getByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: cta })).toBeInTheDocument();
  });

  it('switches language from the header', async () => {
    render(<App i18n={createI18n('ru')} />);

    await userEvent.selectOptions(screen.getByLabelText('Язык'), 'ka');

    expect(await screen.findByRole('heading', { level: 1, name: 'ვიზიტის დაჯავშნა' })).toBeInTheDocument();
  });
});
