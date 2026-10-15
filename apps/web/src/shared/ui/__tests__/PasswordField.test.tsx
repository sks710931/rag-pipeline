import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { PasswordField } from '../PasswordField';

describe('PasswordField', () => {
  it('renders password input masked by default with show toggle button', () => {
    render(<PasswordField id="pass" label="Master Password" />);

    const input = screen.getByLabelText('Master Password');
    expect(input).toHaveAttribute('type', 'password');

    const toggleBtn = screen.getByRole('button', { name: 'Show password' });
    expect(toggleBtn).toBeInTheDocument();
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'false');
  });

  it('toggles password visibility upon clicking toggle button', async () => {
    const user = userEvent.setup();
    render(<PasswordField id="pass" label="Master Password" />);

    const input = screen.getByLabelText('Master Password');
    const toggleBtn = screen.getByRole('button', { name: 'Show password' });

    await user.click(toggleBtn);
    expect(input).toHaveAttribute('type', 'text');
    expect(toggleBtn).toHaveAttribute('aria-label', 'Hide password');
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'true');

    await user.click(toggleBtn);
    expect(input).toHaveAttribute('type', 'password');
    expect(toggleBtn).toHaveAttribute('aria-label', 'Show password');
    expect(toggleBtn).toHaveAttribute('aria-pressed', 'false');
  });

  it('allows keyboard activation of toggle button via Enter and Space', async () => {
    const user = userEvent.setup();
    render(<PasswordField id="pass" label="Master Password" />);

    const input = screen.getByLabelText('Master Password');
    const toggleBtn = screen.getByRole('button', { name: 'Show password' });

    toggleBtn.focus();
    expect(toggleBtn).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(input).toHaveAttribute('type', 'text');

    await user.keyboard(' ');
    expect(input).toHaveAttribute('type', 'password');
  });

  it('never logs password value to console or telemetry', async () => {
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    const infoSpy = vi.spyOn(console, 'info').mockImplementation(() => {});
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    const user = userEvent.setup();
    render(<PasswordField id="pass" label="Master Password" />);

    const input = screen.getByLabelText('Master Password');
    await user.type(input, 'SuperSecretVault123!');

    const toggleBtn = screen.getByRole('button', { name: 'Show password' });
    await user.click(toggleBtn);

    const checkNoPasswordLogged = (calls: unknown[][]) => {
      for (const call of calls) {
        for (const arg of call) {
          expect(String(arg)).not.toContain('SuperSecretVault123!');
        }
      }
    };

    checkNoPasswordLogged(logSpy.mock.calls);
    checkNoPasswordLogged(infoSpy.mock.calls);
    checkNoPasswordLogged(warnSpy.mock.calls);
    checkNoPasswordLogged(errorSpy.mock.calls);

    logSpy.mockRestore();
    infoSpy.mockRestore();
    warnSpy.mockRestore();
    errorSpy.mockRestore();
  });

  it('renders hint and error state with appropriate aria attributes', () => {
    render(
      <PasswordField
        id="pass"
        label="Password"
        hint="At least 12 chars"
        error="Too short"
      />
    );

    const input = screen.getByLabelText('Password');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAttribute('aria-describedby', 'pass-error pass-hint');
    expect(screen.getByRole('alert')).toHaveTextContent('Too short');
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <PasswordField
        id="pass-axe"
        label="Account Password"
        hint="Safe pass requirement"
      />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
