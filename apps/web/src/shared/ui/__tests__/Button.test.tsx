import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { Button } from '../Button';

describe('Button', () => {
  it('renders default primary button with text', () => {
    render(<Button>Click Me</Button>);

    const button = screen.getByRole('button', { name: 'Click Me' });
    expect(button).toBeInTheDocument();
    expect(button).toHaveAttribute('type', 'button');
  });

  it('handles click and keyboard activation via Enter and Space', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(<Button onClick={handleClick}>Trigger Action</Button>);

    const button = screen.getByRole('button', { name: 'Trigger Action' });

    await user.click(button);
    expect(handleClick).toHaveBeenCalledTimes(1);

    button.focus();
    await user.keyboard('{Enter}');
    expect(handleClick).toHaveBeenCalledTimes(2);

    await user.keyboard(' ');
    expect(handleClick).toHaveBeenCalledTimes(3);
  });

  it('renders loading state with aria-busy and prevents click', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(
      <Button loading loadingText="Saving..." onClick={handleClick}>
        Save
      </Button>
    );

    const button = screen.getByRole('button', { name: 'Saving...' });
    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(button).toBeDisabled();

    await user.click(button);
    expect(handleClick).not.toHaveBeenCalled();
  });

  it('disables button when disabled prop is provided', async () => {
    const handleClick = vi.fn();
    const user = userEvent.setup();

    render(
      <Button disabled onClick={handleClick}>
        Disabled
      </Button>
    );

    const button = screen.getByRole('button', { name: 'Disabled' });
    expect(button).toBeDisabled();

    await user.click(button);
    expect(handleClick).not.toHaveBeenCalled();
  });

  it('renders various variants cleanly', () => {
    const { rerender } = render(<Button variant="primary">Btn</Button>);
    expect(screen.getByRole('button')).toHaveClass('bg-brand-600');

    rerender(<Button variant="danger">Btn</Button>);
    expect(screen.getByRole('button')).toHaveClass('bg-error-600');

    rerender(<Button variant="outline">Btn</Button>);
    expect(screen.getByRole('button')).toHaveClass('border-neutral-300');
  });

  it('passes axe accessibility smoke test for all variants', async () => {
    const { container } = render(
      <div>
        <Button variant="primary">Primary</Button>
        <Button variant="secondary">Secondary</Button>
        <Button variant="outline">Outline</Button>
        <Button variant="danger">Danger</Button>
        <Button variant="ghost">Ghost</Button>
        <Button loading loadingText="Busy...">
          Loading
        </Button>
      </div>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
