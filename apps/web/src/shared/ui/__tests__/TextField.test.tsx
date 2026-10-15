import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { TextField } from '../TextField';

describe('TextField', () => {
  it('renders label associated with input via htmlFor/id', () => {
    render(<TextField id="username" label="Username" />);

    const input = screen.getByLabelText('Username');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('id', 'username');
  });

  it('renders hint and links with aria-describedby', () => {
    render(
      <TextField
        id="username"
        label="Username"
        hint="Choose a alphanumeric handle."
      />
    );

    const input = screen.getByLabelText('Username');
    const hint = screen.getByText('Choose a alphanumeric handle.');
    expect(hint).toHaveAttribute('id', 'username-hint');
    expect(input).toHaveAttribute('aria-describedby', 'username-hint');
  });

  it('renders error with alert role, sets aria-invalid, and includes in aria-describedby', () => {
    render(
      <TextField
        id="username"
        label="Username"
        hint="Choose a handle."
        error="Username is required."
      />
    );

    const input = screen.getByLabelText(/username/i);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAttribute(
      'aria-describedby',
      'username-error username-hint'
    );

    const errorAlert = screen.getByRole('alert');
    expect(errorAlert).toHaveTextContent('Username is required.');
  });

  it('allows keyboard typing and value updates', async () => {
    const user = userEvent.setup();
    render(<TextField label="Email" />);

    const input = screen.getByLabelText('Email');
    await user.type(input, 'admin@example.com');
    expect(input).toHaveValue('admin@example.com');
  });

  it('applies disabled state properly', () => {
    render(<TextField label="Disabled Input" disabled />);

    const input = screen.getByLabelText('Disabled Input');
    expect(input).toBeDisabled();
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <TextField
        id="accessible-input"
        label="Accessible Field"
        hint="Helper text"
        error="Validation message"
      />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
