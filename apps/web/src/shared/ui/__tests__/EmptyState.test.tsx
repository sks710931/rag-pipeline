import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { EmptyState } from '../EmptyState';

describe('EmptyState', () => {
  it('renders heading, description, and decorative icon with aria-hidden', () => {
    const { container } = render(
      <EmptyState
        title="No Documents Found"
        description="Try adjusting your search criteria."
      />
    );

    expect(
      screen.getByRole('heading', { name: 'No Documents Found' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Try adjusting your search criteria.')
    ).toBeInTheDocument();

    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders optional real action and handles keyboard operation', async () => {
    const handleAction = vi.fn();
    const user = userEvent.setup();

    render(
      <EmptyState
        title="Empty List"
        action={{
          label: 'Create Project',
          onClick: handleAction,
        }}
      />
    );

    const button = screen.getByRole('button', { name: 'Create Project' });
    expect(button).toBeInTheDocument();

    button.focus();
    expect(button).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(handleAction).toHaveBeenCalledTimes(1);

    await user.keyboard(' ');
    expect(handleAction).toHaveBeenCalledTimes(2);
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <EmptyState
        title="No Records"
        description="Nothing to display here yet."
        action={{
          label: 'Add Record',
          onClick: () => {},
        }}
      />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
