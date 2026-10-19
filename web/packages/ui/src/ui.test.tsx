import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Button, LanguageSelect, Select, TextField } from './index';

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

describe('TextField', () => {
  it('is labelled and reports typing', async () => {
    const onChange = vi.fn();
    render(<TextField id="email" label="Email" value="" onChange={onChange} />);

    await userEvent.type(screen.getByLabelText('Email'), 'a');

    expect(onChange).toHaveBeenCalledWith('a');
  });

  it('shows an error linked to the input', () => {
    render(<TextField id="email" label="Email" value="" onChange={() => {}} error="Fill in this field." />);

    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('Fill in this field.');
  });
});

describe('Select', () => {
  it('works for any list of options', async () => {
    const onChange = vi.fn();
    render(
      <Select
        label="Salon"
        value="a"
        options={[
          { value: 'a', label: 'Salon A' },
          { value: 'b', label: 'Salon B' },
        ]}
        onChange={onChange}
      />,
    );

    await userEvent.selectOptions(screen.getByLabelText('Salon'), 'b');

    expect(onChange).toHaveBeenCalledWith('b');
  });
});
