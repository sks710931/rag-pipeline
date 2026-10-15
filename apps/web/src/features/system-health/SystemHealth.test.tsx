import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SystemHealth } from './SystemHealth';

describe('SystemHealth', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders loading state with status role and aria-busy initially', () => {
    // Keep promise pending so component stays in loading state
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise(() => {})
    );

    render(<SystemHealth />);

    const statusElement = screen.getByRole('status');
    expect(statusElement).toBeInTheDocument();
    expect(statusElement).toHaveAttribute('aria-busy', 'true');
    expect(statusElement).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByText(/checking backend liveness/i)).toBeInTheDocument();
  });

  it('renders success state with reported live status upon 200 OK', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ status: 'live' }),
    } as Response);

    render(<SystemHealth />);

    await waitFor(() => {
      expect(screen.getByText(/backend status:/i)).toBeInTheDocument();
    });

    expect(screen.getByText('live')).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders error state with alert role and functional retry button upon API failure', async () => {
    const user = userEvent.setup();

    // First attempt fails
    vi.spyOn(globalThis, 'fetch')
      .mockRejectedValueOnce(new Error('Network connection refused'))
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ status: 'live' }),
      } as Response);

    render(<SystemHealth />);

    const alertElement = await screen.findByRole('alert');
    expect(alertElement).toBeInTheDocument();
    expect(screen.getByText(/Network connection refused/i)).toBeInTheDocument();

    const retryButton = screen.getByRole('button', { name: /retry/i });
    expect(retryButton).toBeInTheDocument();

    // Click retry
    await user.click(retryButton);

    // Should transition to success
    await waitFor(() => {
      expect(screen.getByText('live')).toBeInTheDocument();
    });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('renders error state when backend returns non-200 HTTP status', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
    } as Response);

    render(<SystemHealth />);

    const alertElement = await screen.findByRole('alert');
    expect(alertElement).toBeInTheDocument();
    expect(
      screen.getByText(/Health check failed with status code 503/i)
    ).toBeInTheDocument();
  });

  it('renders error state when response payload is malformed', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ invalid: true }),
    } as Response);

    render(<SystemHealth />);

    const alertElement = await screen.findByRole('alert');
    expect(alertElement).toBeInTheDocument();
    expect(screen.getByText(/invalid response format/i)).toBeInTheDocument();
  });
});
