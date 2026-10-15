import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { StatusBadge } from '../StatusBadge';

describe('StatusBadge', () => {
  it('renders with role status for success severity and includes non-colour-only text', () => {
    render(<StatusBadge severity="success">Operational</StatusBadge>);

    const badge = screen.getByRole('status');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent(/Success:\s*Operational/);
  });

  it('renders with role alert for error severity and includes non-colour-only text', () => {
    render(<StatusBadge severity="error">System Down</StatusBadge>);

    const badge = screen.getByRole('alert');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent(/Error:\s*System Down/);
  });

  it('renders decorative icon with aria-hidden', () => {
    const { container } = render(
      <StatusBadge severity="warning">Degraded</StatusBadge>
    );

    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveAttribute('aria-hidden', 'true');
  });

  it('passes axe accessibility smoke test across all severities', async () => {
    const { container } = render(
      <div>
        <StatusBadge severity="success">Success</StatusBadge>
        <StatusBadge severity="warning">Warning</StatusBadge>
        <StatusBadge severity="error">Error</StatusBadge>
        <StatusBadge severity="info">Info</StatusBadge>
        <StatusBadge severity="neutral">Neutral</StatusBadge>
      </div>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
