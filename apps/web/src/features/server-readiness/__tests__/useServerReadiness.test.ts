import { renderHook, waitFor, act } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useServerReadiness } from '../useServerReadiness';

describe('useServerReadiness', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with loading status', () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(
      () => new Promise(() => {})
    );

    const { result } = renderHook(() => useServerReadiness());

    expect(result.current.state.status).toBe('loading');
  });

  it('resolves to ready state on 200 OK with status "ready"', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 200,
      json: async () => ({
        status: 'ready',
        dependencies: { database: 'ready' },
      }),
    } as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('ready');
    });

    if (result.current.state.status === 'ready') {
      expect(result.current.state.dependencies).toEqual({ database: 'ready' });
    }
  });

  it('resolves to unavailable state on 503 with dependency report', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 503,
      json: async () => ({
        status: 'not_ready',
        dependencies: { database: 'not_ready' },
      }),
    } as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('unavailable');
    });

    if (result.current.state.status === 'unavailable') {
      expect(result.current.state.message).toContain('currently unavailable');
      expect(result.current.state.dependencies).toEqual({
        database: 'not_ready',
      });
      // Ensure no raw server stack or URLs leaked
      expect(result.current.state.message).not.toMatch(/https?:\/\//);
      expect(result.current.state.message).not.toMatch(/at /);
    }
  });

  it('resolves to unavailable state on 503 when body parsing fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 503,
      json: async () => {
        throw new Error('Not JSON');
      },
    } as unknown as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('unavailable');
    });

    if (result.current.state.status === 'unavailable') {
      expect(result.current.state.message).toContain('currently unavailable');
    }
  });

  it('resolves to unavailable state on network rejection', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(
      new TypeError('Failed to fetch')
    );

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('unavailable');
    });

    if (result.current.state.status === 'unavailable') {
      expect(result.current.state.message).toContain(
        'Unable to reach the server'
      );
      // Sanitized - no raw error message or URLs leaked
      expect(result.current.state.message).not.toContain('Failed to fetch');
    }
  });

  it('resolves to invalid state on 200 with malformed JSON body', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 200,
      json: async () => {
        throw new SyntaxError('Unexpected token');
      },
    } as unknown as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('invalid');
    });

    if (result.current.state.status === 'invalid') {
      expect(result.current.state.message).toContain('invalid or malformed');
    }
  });

  it('resolves to invalid state on 200 with unexpected response structure', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 200,
      json: async () => ({ unexpected: true }),
    } as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('invalid');
    });

    if (result.current.state.status === 'invalid') {
      expect(result.current.state.message).toContain('invalid or malformed');
    }
  });

  it('resolves to invalid state on unexpected HTTP status codes', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce({
      status: 500,
      json: async () => ({ error: 'Internal Server Error' }),
    } as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('invalid');
    });

    if (result.current.state.status === 'invalid') {
      expect(result.current.state.message).toContain(
        'unexpected response status'
      );
    }
  });

  it('supports manual retry via checkReadiness', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockRejectedValueOnce(new TypeError('Network Error'))
      .mockResolvedValueOnce({
        status: 200,
        json: async () => ({ status: 'ready' }),
      } as Response);

    const { result } = renderHook(() => useServerReadiness());

    await waitFor(() => {
      expect(result.current.state.status).toBe('unavailable');
    });

    await act(async () => {
      await result.current.checkReadiness();
    });

    await waitFor(() => {
      expect(result.current.state.status).toBe('ready');
    });
  });

  it('aborts active fetch when unmounting', () => {
    let capturedSignal: AbortSignal | null | undefined;
    vi.spyOn(globalThis, 'fetch').mockImplementation((_url, init) => {
      capturedSignal = init?.signal;
      return new Promise(() => {});
    });

    const { unmount } = renderHook(() => useServerReadiness());

    expect(capturedSignal).toBeDefined();
    expect(capturedSignal?.aborted).toBe(false);

    unmount();

    expect(capturedSignal?.aborted).toBe(true);
  });
});
