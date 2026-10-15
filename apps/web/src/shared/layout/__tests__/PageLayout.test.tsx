import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { PageLayout } from '../PageLayout';
import { Button } from '../../ui/Button';

describe('PageLayout Component', () => {
  it('renders heading, description, and page children', () => {
    render(
      <PageLayout
        heading="Enterprise Dashboard"
        description="Workspace overview and analytics."
      >
        <div data-testid="dashboard-content">Main dashboard view</div>
      </PageLayout>
    );

    const heading = screen.getByRole('heading', {
      level: 2,
      name: 'Enterprise Dashboard',
    });
    expect(heading).toBeInTheDocument();
    expect(heading).toHaveAttribute('tabIndex', '-1');
    expect(
      screen.getByText('Workspace overview and analytics.')
    ).toBeInTheDocument();
    expect(screen.getByTestId('dashboard-content')).toBeInTheDocument();
  });

  it('renders actions slot when provided', () => {
    render(
      <PageLayout
        heading="Settings"
        actions={<Button variant="primary">Save Changes</Button>}
      >
        <div>Settings content</div>
      </PageLayout>
    );

    expect(
      screen.getByRole('button', { name: 'Save Changes' })
    ).toBeInTheDocument();
  });

  it('attaches headingRef to the h2 heading element', () => {
    const headingRef = React.createRef<HTMLHeadingElement>();
    render(<PageLayout heading="Test Heading" headingRef={headingRef} />);

    expect(headingRef.current).toBeInstanceOf(HTMLHeadingElement);
    expect(headingRef.current?.textContent).toBe('Test Heading');
  });

  it('passes automated accessibility audit with zero violations', async () => {
    const { container } = render(
      <PageLayout
        heading="Audit Log"
        description="System activity history."
        actions={<Button variant="outline">Export CSV</Button>}
      >
        <p>Log entries table placeholder</p>
      </PageLayout>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
