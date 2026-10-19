import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Button, LanguageSelect } from './index';

describe('Button', () => {
  it('renders its label and reports clicks', async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClick).toHaveBeenCalledOnce();
  });

  it('does not report clicks while disabled', async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick} disabled>Save</Button>);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClick).not.toHaveBeenCalled();
  });

  it('defaults to type="button" so it never submits a form by accident', () => {
    render(<Button>Save</Button>);
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button');
  });
});

describe('LanguageSelect', () => {
  const options = [
    { value: 'ka', label: 'ქართული' },
    { value: 'en', label: 'English' },
  ];

  it('shows the current language and reports a change', async () => {
    const onChange = vi.fn();
    render(<LanguageSelect label="Language" value="ka" options={options} onChange={onChange} />);

    const select = screen.getByLabelText('Language');
    expect(select).toHaveValue('ka');

    await userEvent.selectOptions(select, 'en');
    expect(onChange).toHaveBeenCalledWith('en');
  });
});
