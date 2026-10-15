import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { App } from './App';

function mockHealthApi({
  readyStatus = 200,
  readyBody = {
    status: 'ready',
    dependencies: { database: 'ready' },
  } as unknown,
  readyError = null as Error | null,
  liveStatus = 200,
  liveBody = { status: 'live' } as unknown,
} = {}) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);
    if (url.includes('/api/v1/system/health/ready')) {
      if (readyError) {
        throw readyError;
      }
      return {
        ok: readyStatus >= 200 && readyStatus < 300,
        status: readyStatus,
        json: async () => {
          if (readyBody === 'MALFORMED_JSON') {
            throw new SyntaxError('Unexpected token');
          }
          return readyBody;
        },
      } as Response;
    }

    if (url.includes('/api/v1/system/health/live')) {
      return {
        ok: liveStatus >= 200 && liveStatus < 300,
        status: liveStatus,
        json: async () => liveBody,
      } as Response;
    }

    return new Response(null, { status: 404 });
  });
}

describe('Public Route Shell and Maintenance UX', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.history.pushState({}, '', '/login');
  });

  describe('Core Shell Landmarks & Accessibility', () => {
    it('renders skip link targeting main-content', async () => {
      mockHealthApi();
      render(<App />);

      const skipLink = screen.getByRole('link', {
        name: /skip to main content/i,
      });
      expect(skipLink).toBeInTheDocument();
      expect(skipLink).toHaveAttribute('href', '#main-content');

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });
    });

    it('renders header landmark with application title and nav links', async () => {
      mockHealthApi();
      render(<App />);

      const banner = screen.getByRole('banner');
      expect(banner).toBeInTheDocument();
      expect(
        screen.getByRole('heading', { level: 1, name: /pixel rag/i })
      ).toBeInTheDocument();

      const nav = screen.getByRole('navigation', { name: /main navigation/i });
      expect(nav).toBeInTheDocument();
      expect(
        screen.getByRole('link', { name: /sign in/i })
      ).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /setup/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /status/i })).toBeInTheDocument();

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });
    });

    it('renders main landmark with matching id and tabIndex', async () => {
      mockHealthApi();
      render(<App />);

      const main = screen.getByRole('main');
      expect(main).toBeInTheDocument();
      expect(main).toHaveAttribute('id', 'main-content');
      expect(main).toHaveAttribute('tabIndex', '-1');

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });
    });

    it('never contains owner creation form, nav link, or create-owner text anywhere', async () => {
      mockHealthApi();
      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });

      const bodyText = container.textContent?.toLowerCase() ?? '';
      expect(bodyText).not.toContain('create-owner');
      expect(bodyText).not.toContain('create owner');
      expect(
        screen.queryByRole('link', { name: /owner/i })
      ).not.toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: /owner/i })
      ).not.toBeInTheDocument();

      // Ensure no bootstrap endpoints were contacted
      expect(globalThis.fetch).not.toHaveBeenCalledWith(
        expect.stringContaining('bootstrap'),
        expect.anything()
      );
    });
  });

  describe('Route: / (Root Redirect)', () => {
    it('redirects / to /login', async () => {
      mockHealthApi();
      window.history.pushState({}, '', '/');

      render(<App />);

      await waitFor(() => {
        expect(window.location.pathname).toBe('/login');
        expect(document.title).toBe('Sign In | Pixel RAG');
      });
    });
  });

  describe('Route: /login', () => {
    it('renders loading state with Skeleton, aria-busy, and polite status', () => {
      vi.spyOn(globalThis, 'fetch').mockImplementation(
        () => new Promise(() => {})
      );
      window.history.pushState({}, '', '/login');

      render(<App />);

      const statusElement = screen.getByRole('status', { busy: true });
      expect(statusElement).toBeInTheDocument();
      expect(statusElement).toHaveAttribute('aria-busy', 'true');
      expect(statusElement).toHaveAttribute('aria-live', 'polite');
      expect(
        screen.getByText(/checking server readiness/i)
      ).toBeInTheDocument();
    });

    it('renders neutral screen when server readiness probe succeeds (200 OK)', async () => {
      mockHealthApi({
        readyStatus: 200,
        readyBody: { status: 'ready', dependencies: { database: 'ready' } },
      });
      window.history.pushState({}, '', '/login');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });

      expect(
        screen.getByText(/sign-in functionality is not yet available/i)
      ).toBeInTheDocument();
      expect(screen.getByText(/^ready$/i)).toBeInTheDocument();
      expect(document.title).toBe('Sign In | Pixel RAG');

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('renders maintenance panel with alert role and focused heading on 503 Service Unavailable', async () => {
      mockHealthApi({
        readyStatus: 503,
        readyBody: {
          status: 'not_ready',
          dependencies: { database: 'not_ready' },
        },
      });
      window.history.pushState({}, '', '/login');

      const { container } = render(<App />);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();

      const maintenanceHeading = screen.getByRole('heading', {
        name: /system maintenance/i,
      });
      expect(maintenanceHeading).toBeInTheDocument();
      expect(maintenanceHeading).toHaveFocus();

      expect(
        screen.getByText(/server is currently unavailable/i)
      ).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: /retry/i })
      ).toBeInTheDocument();

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('renders maintenance panel on network failure and handles retry recovery', async () => {
      const user = userEvent.setup();
      const fetchSpy = vi
        .spyOn(globalThis, 'fetch')
        .mockRejectedValueOnce(new TypeError('Connection refused'))
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ status: 'ready' }),
        } as Response);

      window.history.pushState({}, '', '/login');
      const { container } = render(<App />);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(
        screen.getByText(/unable to reach the server/i)
      ).toBeInTheDocument();

      // Ensure no raw exception message / stack trace leaked
      expect(container.textContent).not.toContain('Connection refused');

      const retryButton = screen.getByRole('button', { name: /retry/i });
      expect(retryButton).toBeInTheDocument();

      await user.click(retryButton);

      await waitFor(() => {
        expect(
          screen.getByText(/sign-in functionality is not yet available/i)
        ).toBeInTheDocument();
      });

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(fetchSpy).toHaveBeenCalledTimes(2);
    });

    it('renders maintenance panel on malformed JSON response body', async () => {
      mockHealthApi({
        readyStatus: 200,
        readyBody: 'MALFORMED_JSON',
      });
      window.history.pushState({}, '', '/login');

      const { container } = render(<App />);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(
        screen.getByText(/invalid or malformed response format/i)
      ).toBeInTheDocument();

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Route: /setup', () => {
    it('renders neutral setup screen when ready without owner creation form', async () => {
      mockHealthApi({
        readyStatus: 200,
        readyBody: { status: 'ready', dependencies: { database: 'ready' } },
      });
      window.history.pushState({}, '', '/setup');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /system setup/i })
        ).toBeInTheDocument();
      });

      expect(document.title).toBe('Setup | Pixel RAG');
      expect(
        screen.getByText(/initial setup is not yet available/i)
      ).toBeInTheDocument();
      expect(screen.getByText(/^ready$/i)).toBeInTheDocument();

      // Strict check: No inputs, no owner creation text anywhere
      expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
      expect(container.textContent?.toLowerCase()).not.toContain(
        'create-owner'
      );
      expect(container.textContent?.toLowerCase()).not.toContain(
        'create owner'
      );

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('renders maintenance panel on /setup when server is 503 unavailable', async () => {
      mockHealthApi({
        readyStatus: 503,
        readyBody: { status: 'not_ready' },
      });
      window.history.pushState({}, '', '/setup');

      const { container } = render(<App />);

      const alert = await screen.findByRole('alert');
      expect(alert).toBeInTheDocument();

      const heading = screen.getByRole('heading', {
        name: /system maintenance/i,
      });
      expect(heading).toHaveFocus();

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Route: /maintenance', () => {
    it('renders actionable maintenance screen when in maintenance mode', async () => {
      mockHealthApi({
        readyStatus: 503,
        readyBody: {
          status: 'not_ready',
          dependencies: { database: 'not_ready' },
        },
      });
      window.history.pushState({}, '', '/maintenance');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /system maintenance/i })
        ).toBeInTheDocument();
      });

      expect(document.title).toBe('Maintenance | Pixel RAG');
      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: /retry/i })
      ).toBeInTheDocument();

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('renders operational state with link to sign in when server is ready', async () => {
      mockHealthApi({
        readyStatus: 200,
        readyBody: { status: 'ready' },
      });
      window.history.pushState({}, '', '/maintenance');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByText(/all systems operational/i)
        ).toBeInTheDocument();
      });

      const signInLink = screen.getByRole('link', {
        name: /proceed to sign in/i,
      });
      expect(signInLink).toBeInTheDocument();
      expect(signInLink).toHaveAttribute('href', '/login');

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Route: /status', () => {
    it('renders system-health feature probe on /status route', async () => {
      mockHealthApi({
        liveStatus: 200,
        liveBody: { status: 'live' },
      });
      window.history.pushState({}, '', '/status');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /system status/i })
        ).toBeInTheDocument();
      });

      expect(document.title).toBe('System Status | Pixel RAG');
      await waitFor(() => {
        expect(screen.getByText(/backend status:/i)).toBeInTheDocument();
        expect(screen.getByText('live')).toBeInTheDocument();
      });

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Route: Unknown Paths (404 NotFound)', () => {
    it('renders NotFound screen with home navigation link to prevent dead nav', async () => {
      mockHealthApi();
      window.history.pushState({}, '', '/some/nonexistent/route');

      const { container } = render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /page not found/i })
        ).toBeInTheDocument();
      });

      expect(document.title).toBe('Page Not Found | Pixel RAG');

      const returnLink = screen.getByRole('link', { name: /return home/i });
      expect(returnLink).toBeInTheDocument();
      expect(returnLink).toHaveAttribute('href', '/');

      // Accessibility check
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Navigation, Focus Movement & Real Browser Back', () => {
    it('moves focus to the main heading when navigating between routes', async () => {
      const user = userEvent.setup();
      mockHealthApi({
        readyStatus: 200,
        readyBody: { status: 'ready' },
      });
      window.history.pushState({}, '', '/login');

      render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });

      const loginHeading = screen.getByRole('heading', { name: /sign in/i });
      expect(loginHeading).toHaveFocus();

      // Click "Setup" in header nav
      const setupNavLink = screen.getByRole('link', { name: /^setup$/i });
      await user.click(setupNavLink);

      await waitFor(() => {
        expect(window.location.pathname).toBe('/setup');
      });

      const setupHeading = screen.getByRole('heading', {
        name: /system setup/i,
      });
      expect(setupHeading).toBeInTheDocument();
      expect(setupHeading).toHaveFocus();
      expect(document.title).toBe('Setup | Pixel RAG');
    });

    it('supports real browser back button navigation', async () => {
      const user = userEvent.setup();
      mockHealthApi({
        readyStatus: 200,
        readyBody: { status: 'ready' },
      });
      window.history.pushState({}, '', '/login');

      render(<App />);

      await waitFor(() => {
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });

      // Navigate to /setup
      const setupNavLink = screen.getByRole('link', { name: /^setup$/i });
      await user.click(setupNavLink);

      await waitFor(() => {
        expect(window.location.pathname).toBe('/setup');
      });

      // Trigger browser back
      await act(async () => {
        window.history.back();
      });

      await waitFor(() => {
        expect(window.location.pathname).toBe('/login');
        expect(
          screen.getByRole('heading', { name: /sign in/i })
        ).toBeInTheDocument();
      });
    });
  });
});
