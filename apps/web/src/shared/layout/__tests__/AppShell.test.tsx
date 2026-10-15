import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { axe } from 'vitest-axe';
import { AppShell } from '../AppShell';
import { PageLayout } from '../PageLayout';
import { Button } from '../../ui/Button';

const REGISTERED_ROUTES = ['/', '/login', '/setup', '/maintenance', '/status'];

function TestApp({
  session,
  initialPath = '/status',
}: {
  session?: Parameters<typeof AppShell>[0]['session'];
  initialPath?: string;
}) {
  return (
    <MemoryRouter initialEntries={[initialPath]}>
      <AppShell session={session}>
        <Routes>
          <Route
            path="/status"
            element={
              <PageLayout
                heading="System Status"
                description="Status probe page."
              >
                <div>Status Content</div>
              </PageLayout>
            }
          />
          <Route
            path="/login"
            element={
              <PageLayout heading="Sign In" description="Sign in page.">
                <div>Sign In Content</div>
              </PageLayout>
            }
          />
          <Route
            path="/setup"
            element={
              <PageLayout heading="Setup" description="Setup page.">
                <div>Setup Content</div>
              </PageLayout>
            }
          />
          <Route
            path="/maintenance"
            element={
              <PageLayout heading="Maintenance" description="Maintenance page.">
                <div>Maintenance Content</div>
              </PageLayout>
            }
          />
          <Route
            path="/"
            element={
              <PageLayout heading="Home" description="Home page.">
                <div>Home Content</div>
              </PageLayout>
            }
          />
          <Route
            path="*"
            element={
              <PageLayout
                heading="Not Found"
                description="Page does not exist."
              >
                <div>Not Found Content</div>
              </PageLayout>
            }
          />
        </Routes>
      </AppShell>
    </MemoryRouter>
  );
}

describe('AppShell: Truthful Navigation & Responsive Architecture', () => {
  describe('Route and Capability Truth', () => {
    it('ensures every interactive nav link maps strictly to a registered route', () => {
      render(<TestApp initialPath="/status" />);

      // Query all links inside navigation landmarks
      const navigations = screen.getAllByRole('navigation');
      const allNavLinks: HTMLAnchorElement[] = [];

      for (const nav of navigations) {
        const links = within(nav).queryAllByRole('link');
        for (const link of links) {
          allNavLinks.push(link as HTMLAnchorElement);
        }
      }

      expect(allNavLinks.length).toBeGreaterThan(0);

      for (const link of allNavLinks) {
        const href = link.getAttribute('href');
        expect(href).toBeTruthy();
        expect(REGISTERED_ROUTES).toContain(href);
      }
    });

    it('renders planned items as non-interactive text with visible "Not available yet" description', () => {
      render(<TestApp initialPath="/status" />);

      const plannedLabels = ['Documents', 'Search', 'OCR'];

      for (const label of plannedLabels) {
        // Query elements with this text
        const textElements = screen.getAllByText(label);
        expect(textElements.length).toBeGreaterThan(0);

        // Verify planned items are NEVER links or buttons
        expect(
          screen.queryByRole('link', { name: new RegExp(label, 'i') })
        ).not.toBeInTheDocument();
        expect(
          screen.queryByRole('button', { name: new RegExp(label, 'i') })
        ).not.toBeInTheDocument();

        // Check the planned wrapper element has aria-disabled="true"
        for (const el of textElements) {
          const wrapper = el.closest('[aria-disabled="true"]');
          expect(wrapper).toBeInTheDocument();
          expect(wrapper).not.toHaveAttribute('tabindex');
          expect(wrapper?.tagName).not.toBe('BUTTON');
          expect(wrapper?.tagName).not.toBe('A');
        }
      }

      // Check visible "Not available yet" descriptions
      const descriptions = screen.getAllByText(/not available yet/i);
      expect(descriptions.length).toBeGreaterThanOrEqual(3);
    });

    it('contains no dead buttons anywhere in the shell', () => {
      render(<TestApp initialPath="/status" />);

      const buttons = screen.getAllByRole('button');
      for (const button of buttons) {
        // Every button must have accessible name and not be a placeholder
        expect(button).toHaveAccessibleName();
        expect(button.textContent?.toLowerCase()).not.toContain('placeholder');
      }
    });
  });

  describe('Anonymous Session Never Shows Tenant / User Data', () => {
    it('never renders tenant or user sections for anonymous sessions', () => {
      const { container } = render(
        <TestApp session={{ kind: 'anonymous' }} initialPath="/status" />
      );

      // Verify no testids for tenant/user are present
      expect(
        screen.queryByTestId('sidebar-tenant-user')
      ).not.toBeInTheDocument();
      expect(
        screen.queryByTestId('header-tenant-user')
      ).not.toBeInTheDocument();
      expect(
        screen.queryByTestId('mobile-tenant-user')
      ).not.toBeInTheDocument();

      // Check no fabricated user/tenant text in body
      const bodyText = container.textContent?.toLowerCase() ?? '';
      expect(bodyText).not.toContain('acme');
      expect(bodyText).not.toContain('default tenant');
      expect(bodyText).not.toContain('guest user');
      expect(bodyText).not.toContain('signed in as');
    });

    it('renders tenant and user information only when authenticated session is provided', () => {
      render(
        <TestApp
          session={{
            kind: 'authenticated',
            tenantName: 'Cyberdyne Systems',
            userDisplayName: 'Sarah Connor',
          }}
          initialPath="/status"
        />
      );

      const sidebarTenantUser = screen.getByTestId('sidebar-tenant-user');
      expect(sidebarTenantUser).toBeInTheDocument();
      expect(sidebarTenantUser).toHaveTextContent('Cyberdyne Systems');
      expect(sidebarTenantUser).toHaveTextContent('Sarah Connor');
    });
  });

  describe('Mobile Drawer: Open, Close, Escape, and Focus Return', () => {
    it('opens mobile nav drawer on trigger click and updates aria-expanded', async () => {
      const user = userEvent.setup();
      render(<TestApp initialPath="/status" />);

      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      expect(menuButton).toHaveAttribute('aria-expanded', 'false');
      expect(menuButton).toHaveAttribute('aria-controls', 'mobile-nav-drawer');

      await user.click(menuButton);

      expect(menuButton).toHaveAttribute('aria-expanded', 'true');
      expect(
        screen.getByRole('dialog', { name: /navigation/i })
      ).toBeInTheDocument();
    });

    it('closes mobile nav drawer via close button and returns focus to trigger', async () => {
      const user = userEvent.setup();
      render(<TestApp initialPath="/status" />);

      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      await user.click(menuButton);

      const closeButton = screen.getByRole('button', {
        name: /close navigation drawer/i,
      });
      await user.click(closeButton);

      expect(
        screen.queryByRole('dialog', { name: /navigation/i })
      ).not.toBeInTheDocument();
      expect(menuButton).toHaveAttribute('aria-expanded', 'false');
      expect(menuButton).toHaveFocus();
    });

    it('closes mobile nav drawer on Escape key and returns focus to trigger', async () => {
      const user = userEvent.setup();
      render(<TestApp initialPath="/status" />);

      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      await user.click(menuButton);

      expect(
        screen.getByRole('dialog', { name: /navigation/i })
      ).toBeInTheDocument();

      await user.keyboard('{Escape}');

      expect(
        screen.queryByRole('dialog', { name: /navigation/i })
      ).not.toBeInTheDocument();
      expect(menuButton).toHaveAttribute('aria-expanded', 'false');
      expect(menuButton).toHaveFocus();
    });

    it('closes mobile nav drawer on route change and returns focus to trigger', async () => {
      const user = userEvent.setup();
      render(<TestApp initialPath="/login" />);

      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      await user.click(menuButton);

      const mobileNav = screen.getByRole('navigation', {
        name: 'Mobile Navigation',
      });
      const statusLink = within(mobileNav).getByRole('link', {
        name: /system status/i,
      });

      await user.click(statusLink);

      // Drawer must automatically close
      expect(
        screen.queryByRole('dialog', { name: /navigation/i })
      ).not.toBeInTheDocument();
      expect(menuButton).toHaveAttribute('aria-expanded', 'false');
      expect(menuButton).toHaveFocus();
    });
  });

  describe('Keyboard Tab Order', () => {
    it('follows accessible tab order and skips non-interactive planned items', async () => {
      const user = userEvent.setup();
      render(<TestApp initialPath="/status" />);

      // Tab 1: Skip link
      await user.tab();
      const skipLink = screen.getByRole('link', {
        name: /skip to main content/i,
      });
      expect(skipLink).toHaveFocus();

      // Tab 2: Menu button
      await user.tab();
      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      expect(menuButton).toHaveFocus();

      // Tab 3: Brand link
      await user.tab();
      const brandLink = screen.getByRole('link', { name: /pixel rag/i });
      expect(brandLink).toHaveFocus();

      // Tab 4: Header Sign In link
      await user.tab();
      const mainNav = screen.getByRole('navigation', {
        name: 'Main Navigation',
      });
      const signInLink = within(mainNav).getByRole('link', {
        name: /sign in/i,
      });
      expect(signInLink).toHaveFocus();

      // Tab 5: Header Setup link
      await user.tab();
      const setupLink = within(mainNav).getByRole('link', { name: /setup/i });
      expect(setupLink).toHaveFocus();

      // Tab 6: Header Status link
      await user.tab();
      const statusLink = within(mainNav).getByRole('link', {
        name: /status/i,
      });
      expect(statusLink).toHaveFocus();

      // Tab 7: Sidebar System Status link
      await user.tab();
      const sidebarNav = screen.getByRole('navigation', {
        name: 'Sidebar Navigation',
      });
      const sidebarStatusLink = within(sidebarNav).getByRole('link', {
        name: /system status/i,
      });
      expect(sidebarStatusLink).toHaveFocus();

      // Tab 8: Tab forward must SKIP Documents, Search, OCR, and focus Breadcrumbs Home link!
      await user.tab();
      const breadcrumbsNav = screen.getByRole('navigation', {
        name: 'Breadcrumb',
      });
      const breadcrumbHome = within(breadcrumbsNav).getByRole('link', {
        name: 'Home',
      });
      expect(breadcrumbHome).toHaveFocus();

      // Verify that Documents, Search, OCR were NEVER focused
      expect(document.activeElement?.textContent).not.toContain('Documents');
      expect(document.activeElement?.textContent).not.toContain('Search');
      expect(document.activeElement?.textContent).not.toContain('OCR');
    });
  });

  describe('Breadcrumbs Navigation', () => {
    it('renders breadcrumbs with aria-label="Breadcrumb" and marks page with aria-current="page"', () => {
      render(<TestApp initialPath="/status" />);

      const nav = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(nav).toBeInTheDocument();

      const homeLink = within(nav).getByRole('link', { name: 'Home' });
      expect(homeLink).toHaveAttribute('href', '/');

      const currentSpan = within(nav).getByText('System Status');
      expect(currentSpan).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('Automated Accessibility Audits (vitest-axe)', () => {
    it('has zero accessibility violations in default shell state', async () => {
      const { container } = render(<TestApp initialPath="/status" />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has zero accessibility violations with mobile drawer open', async () => {
      const user = userEvent.setup();
      const { container } = render(<TestApp initialPath="/status" />);

      const menuButton = screen.getByRole('button', {
        name: /open navigation menu/i,
      });
      await user.click(menuButton);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has zero accessibility violations with authenticated session', async () => {
      const { container } = render(
        <TestApp
          session={{
            kind: 'authenticated',
            tenantName: 'Apex Health Corp',
            userDisplayName: 'Admin User',
          }}
          initialPath="/status"
        />
      );
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has zero accessibility violations with PageLayout and actions slot', async () => {
      const { container } = render(
        <MemoryRouter initialEntries={['/custom']}>
          <AppShell session={{ kind: 'anonymous' }}>
            <PageLayout
              heading="Workspace Management"
              description="Configure organization settings and tenant policies."
              actions={
                <div className="flex gap-2">
                  <Button variant="outline">Discard</Button>
                  <Button variant="primary">Apply</Button>
                </div>
              }
            >
              <div>Settings form</div>
            </PageLayout>
          </AppShell>
        </MemoryRouter>
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });
});
