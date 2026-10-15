import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { Skeleton } from '../Skeleton';

describe('Skeleton', () => {
  it('renders decorative placeholder with aria-hidden and visually hidden status', () => {
    const { container } = render(
      <Skeleton width="100px" height="20px" loadingText="Loading profile..." />
    );

    const decorativeElement = container.querySelector('[aria-hidden="true"]');
    expect(decorativeElement).toBeInTheDocument();
    expect(decorativeElement).toHaveStyle({ width: '100px', height: '20px' });

    const statusElement = screen.getByRole('status');
    expect(statusElement).toBeInTheDocument();
    expect(statusElement).toHaveClass('sr-only');
    expect(statusElement).toHaveAttribute('aria-live', 'polite');
    expect(statusElement).toHaveTextContent('Loading profile...');
  });

  it('renders rounded-full when circle prop is true', () => {
    const { container } = render(<Skeleton circle width="40px" />);

    const decorativeElement = container.querySelector('[aria-hidden="true"]');
    expect(decorativeElement).toHaveClass('rounded-full');
  });

  it('passes axe accessibility smoke test', async () => {
    const { container } = render(
      <div>
        <Skeleton width="200px" height="16px" />
        <Skeleton circle width="32px" />
      </div>
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
