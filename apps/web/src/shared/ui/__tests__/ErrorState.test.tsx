import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { ErrorState } from '../ErrorState';

describe('ErrorState', () => {
  it('renders alert role, heading, description, and error details', () => {
    const { container } = render(
      <ErrorState
        title="Connection Failed"
        description="Could not reach authorization service."
        errorDetails="STATUS_CODE: 504_GATEWAY_TIMEOUT"
      />
    );

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Connection Failed' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Could not reach authorization service.')
    ).toBeInTheDocument();
    expect(
      screen.getByText('STATUS_CODE: 504_GATEWAY_TIMEOUT')
    ).toBeInTheDocument();

    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders optional action button with keyboard operability', async () => {
    const handleRetry = vi.fn();
    const user = userEvent.setup();

    render(
      <ErrorState
        title="Service Error"
        action={{
          label: 'Retry Request',
          onClick: handleRetry,
        }}
      />
    );

    const retryBtn = screen.getByRole('button', { name: 'Retry Request' });
    expect(retryBtn).toBeInTheDocument();

    retryBtn.focus();
    expect(retryBtn).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(handleRetry).toHaveBeenCalledTimes(1);

    await user.keyboard(' ');
    expect(handleRetry).toHaveBeenCalledTimes(2);
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <ErrorState
        title="Service Outage"
        description="Service unavailable."
        action={{
          label: 'Retry Outage',
          onClick: () => {},
        }}
      />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
