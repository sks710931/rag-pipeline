import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { Stepper, type StepItem } from '../Stepper';

const sampleSteps: StepItem[] = [
  { id: 1, title: 'Configuration', description: 'Setup database' },
  { id: 2, title: 'Credentials', description: 'Admin user' },
  { id: 3, title: 'Completion', description: 'Review summary' },
];

describe('Stepper', () => {
  it('renders an ordered list with navigation semantics', () => {
    render(<Stepper steps={sampleSteps} currentStepIndex={1} />);

    const nav = screen.getByRole('navigation', { name: 'Progress steps' });
    expect(nav).toBeInTheDocument();

    const list = screen.getByRole('list');
    expect(list.tagName.toLowerCase()).toBe('ol');

    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
  });

  it('sets aria-current=step on the active step', () => {
    render(<Stepper steps={sampleSteps} currentStepIndex={1} />);

    const items = screen.getAllByRole('listitem');
    expect(items[0]).not.toHaveAttribute('aria-current');
    expect(items[1]).toHaveAttribute('aria-current', 'step');
    expect(items[2]).not.toHaveAttribute('aria-current');
  });

  it('includes non-colour-only textual state indicators for each step', () => {
    render(<Stepper steps={sampleSteps} currentStepIndex={1} />);

    expect(screen.getByText('(Completed)')).toBeInTheDocument();
    expect(screen.getByText('(Current step)')).toBeInTheDocument();
    expect(screen.getByText('(Upcoming)')).toBeInTheDocument();
  });

  it('supports interactive keyboard navigation when onStepClick is provided', async () => {
    const handleStepClick = vi.fn();
    const user = userEvent.setup();

    render(
      <Stepper
        steps={sampleSteps}
        currentStepIndex={0}
        onStepClick={handleStepClick}
      />
    );

    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(3);

    buttons[1]?.focus();
    expect(buttons[1]).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(handleStepClick).toHaveBeenCalledWith(1);

    await user.keyboard(' ');
    expect(handleStepClick).toHaveBeenCalledWith(1);
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <Stepper steps={sampleSteps} currentStepIndex={1} />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
