import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { StatusMessage } from '../StatusMessage';

describe('StatusMessage', () => {
  it('renders with role status for info/warning/success severities', () => {
    const { rerender } = render(
      <StatusMessage severity="info" title="Information">
        Update available
      </StatusMessage>
    );

    expect(screen.getByRole('status')).toBeInTheDocument();

    rerender(
      <StatusMessage severity="success" title="Completed">
        All done
      </StatusMessage>
    );
    expect(screen.getByRole('status')).toBeInTheDocument();

    rerender(
      <StatusMessage severity="warning" title="Warning">
        Check disk usage
      </StatusMessage>
    );
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('renders with role alert for error severity', () => {
    render(
      <StatusMessage severity="error" title="Critical Failure">
        Database connection lost.
      </StatusMessage>
    );

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent('Critical Failure');
    expect(alert).toHaveTextContent('Database connection lost.');
  });

  it('renders action slot cleanly', () => {
    render(
      <StatusMessage
        severity="error"
        title="Error"
        action={<button type="button">Retry Action</button>}
      >
        Failed request.
      </StatusMessage>
    );

    expect(
      screen.getByRole('button', { name: 'Retry Action' })
    ).toBeInTheDocument();
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <div>
        <StatusMessage severity="info" title="Info Title">
          Information body
        </StatusMessage>
        <StatusMessage severity="error" title="Error Title">
          Error description
        </StatusMessage>
      </div>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
