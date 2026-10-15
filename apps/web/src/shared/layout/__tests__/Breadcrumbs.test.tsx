import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { Breadcrumbs } from '../Breadcrumbs';

describe('Breadcrumbs Component', () => {
  it('renders breadcrumb landmark with aria-label="Breadcrumb"', () => {
    render(
      <MemoryRouter initialEntries={['/status']}>
        <Breadcrumbs />
      </MemoryRouter>
    );

    const nav = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(nav).toBeInTheDocument();
  });

  it('marks current page with aria-current="page" as non-interactive text', () => {
    render(
      <MemoryRouter initialEntries={['/status']}>
        <Breadcrumbs />
      </MemoryRouter>
    );

    const currentPage = screen.getByText('System Status');
    expect(currentPage).toHaveAttribute('aria-current', 'page');
    expect(currentPage.tagName).toBe('SPAN');
    expect(
      screen.queryByRole('link', { name: 'System Status' })
    ).not.toBeInTheDocument();
  });

  it('renders ancestor breadcrumbs as navigation links', () => {
    render(
      <MemoryRouter initialEntries={['/status']}>
        <Breadcrumbs />
      </MemoryRouter>
    );

    const homeLink = screen.getByRole('link', { name: 'Home' });
    expect(homeLink).toBeInTheDocument();
    expect(homeLink).toHaveAttribute('href', '/');
  });

  it('renders custom items when provided via items prop', () => {
    const customItems = [
      { label: 'Workspaces', path: '/workspaces' },
      { label: 'Project Alpha', path: '/workspaces/alpha' },
      { label: 'Documents' },
    ];

    render(
      <MemoryRouter initialEntries={['/workspaces/alpha/docs']}>
        <Breadcrumbs items={customItems} />
      </MemoryRouter>
    );

    expect(screen.getByRole('link', { name: 'Workspaces' })).toHaveAttribute(
      'href',
      '/workspaces'
    );
    expect(screen.getByRole('link', { name: 'Project Alpha' })).toHaveAttribute(
      'href',
      '/workspaces/alpha'
    );
    const activeItem = screen.getByText('Documents');
    expect(activeItem).toHaveAttribute('aria-current', 'page');
  });

  it('passes automated accessibility audit with zero violations', async () => {
    const { container } = render(
      <MemoryRouter initialEntries={['/status']}>
        <Breadcrumbs />
      </MemoryRouter>
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
