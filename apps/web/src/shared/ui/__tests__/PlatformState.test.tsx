import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { PlatformState, type PlatformStateType } from '../PlatformState';

describe('PlatformState component', () => {
  const allStates: PlatformStateType[] = [
    'error',
    'loading',
    'forbidden',
    'empty',
    'expired-session',
    'recovery-pending',
    'success',
  ];

  it.each(allStates)(
    'renders state "%s" and passes axe accessibility audit',
    async (stateType) => {
      const { container } = render(
        <PlatformState
          type={stateType}
          action={{
            label: 'Acknowledge',
            onClick: () => {},
          }}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    }
  );

  it('renders error state with role="alert" and error details', () => {
    render(
      <PlatformState
        type="error"
        title="Database Connection Lost"
        description="The primary PostgreSQL cluster is unresponsive on port 5432."
        details="Npgsql.NpgsqlException: Connection refused (0x0000274D)"
      />
    );

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(
      screen.getByRole('heading', {
        level: 3,
        name: /database connection lost/i,
      })
    ).toBeInTheDocument();
    expect(
      screen.getByText(/primary postgresql cluster is unresponsive/i)
    ).toBeInTheDocument();
    expect(screen.getByText(/Npgsql\.NpgsqlException/i)).toBeInTheDocument();
  });

  it('renders loading state with role="status", aria-live, and aria-busy', () => {
    render(
      <PlatformState
        type="loading"
        title="Syncing Index Generation..."
        description="Calculating vector projections for workspace collections."
      />
    );

    const status = screen.getByRole('status');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status).toHaveAttribute('aria-busy', 'true');
    expect(
      screen.getByRole('heading', {
        level: 3,
        name: /syncing index generation/i,
      })
    ).toBeInTheDocument();
  });

  it('renders forbidden state with access denied messaging', () => {
    render(
      <PlatformState
        type="forbidden"
        title="Tenant Isolation Restricted"
        description="Current user lacks the required TenantAdmin role to modify key protector settings."
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', {
        level: 3,
        name: /tenant isolation restricted/i,
      })
    ).toBeInTheDocument();
    expect(
      screen.getByText(/lacks the required tenantadmin role/i)
    ).toBeInTheDocument();
  });

  it('renders empty state with role="region"', () => {
    render(
      <PlatformState
        type="empty"
        title="No Processing Runs"
        description="No extraction jobs have been scheduled for this document version."
      />
    );

    expect(screen.getByRole('region')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 3, name: /no processing runs/i })
    ).toBeInTheDocument();
  });

  it('renders expired-session state with call-to-action button', async () => {
    const handleLogin = vi.fn();
    const user = userEvent.setup();

    render(
      <PlatformState
        type="expired-session"
        title="Session Expired"
        description="Your security session has expired. Re-authenticate to resume."
        action={{
          label: 'Sign In Again',
          onClick: handleLogin,
        }}
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    const actionBtn = screen.getByRole('button', { name: /sign in again/i });
    expect(actionBtn).toBeInTheDocument();

    await user.click(actionBtn);
    expect(handleLogin).toHaveBeenCalledTimes(1);
  });

  it('renders recovery-pending state with administrator instructions', () => {
    render(
      <PlatformState
        type="recovery-pending"
        title="Key Protector Locked"
        description="Hardware TPM state changed. Upload offline recovery envelope to re-activate master encryption keys."
        details="STATUS: LOCKED/RECOVERY_REQUIRED | Protector ID: tpm-cng-01"
      />
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 3, name: /key protector locked/i })
    ).toBeInTheDocument();
    expect(screen.getByText(/LOCKED\/RECOVERY_REQUIRED/i)).toBeInTheDocument();
  });

  it('renders success state with role="status"', () => {
    render(
      <PlatformState
        type="success"
        title="Key Rotation Completed"
        description="All secret envelopes were successfully re-wrapped with key version 2."
      />
    );

    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { level: 3, name: /key rotation completed/i })
    ).toBeInTheDocument();
  });

  it('manages auto-focus on heading when autoFocusHeading is true', () => {
    render(
      <PlatformState
        type="error"
        title="Focusable Alert Title"
        autoFocusHeading
      />
    );

    const heading = screen.getByRole('heading', {
      level: 3,
      name: /focusable alert title/i,
    });
    expect(heading).toHaveFocus();
    expect(heading).toHaveAttribute('tabindex', '-1');
  });

  it('supports custom heading levels (h1, h2, h4)', () => {
    const { rerender } = render(
      <PlatformState type="error" headingLevel="h1" title="Top Level Error" />
    );
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument();

    rerender(
      <PlatformState type="error" headingLevel="h2" title="Section Error" />
    );
    expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument();

    rerender(
      <PlatformState type="error" headingLevel="h4" title="Sub-section Error" />
    );
    expect(screen.getByRole('heading', { level: 4 })).toBeInTheDocument();
  });
});
