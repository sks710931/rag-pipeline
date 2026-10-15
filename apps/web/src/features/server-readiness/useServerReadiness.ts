import { useCallback, useEffect, useRef, useState } from 'react';

export type ServerReadinessStatus =
  | 'loading'
  | 'ready'
  | 'unavailable'
  | 'invalid';

interface ReadinessResponseBody {
  status?: unknown;
  dependencies?: unknown;
}

export type ServerReadinessState =
  | { status: 'loading' }
  | { status: 'ready'; dependencies?: Record<string, string> }
  | {
      status: 'unavailable';
      message: string;
      dependencies?: Record<string, string>;
    }
  | { status: 'invalid'; message: string };

export interface UseServerReadinessReturn {
  state: ServerReadinessState;
  checkReadiness: () => Promise<void>;
}

export function useServerReadiness(): UseServerReadinessReturn {
  const [state, setState] = useState<ServerReadinessState>({
    status: 'loading',
  });
  const activeControllerRef = useRef<AbortController | null>(null);

  const checkReadiness = useCallback(async () => {
    // Abort previous in-flight request if any
    activeControllerRef.current?.abort();
    const controller = new AbortController();
    activeControllerRef.current = controller;

    setState({ status: 'loading' });

    try {
      const response = await fetch('/api/v1/system/health/ready', {
        signal: controller.signal,
        headers: { Accept: 'application/json' },
      });

      if (response.status === 200) {
        let body: unknown;
        try {
          body = await response.json();
        } catch {
          if (!controller.signal.aborted) {
            setState({
              status: 'invalid',
              message:
                'The server returned an invalid or malformed response format.',
            });
          }
          return;
        }

        if (
          typeof body === 'object' &&
          body !== null &&
          (body as ReadinessResponseBody).status === 'ready'
        ) {
          const rawDeps = (body as ReadinessResponseBody).dependencies;
          const dependencies =
            typeof rawDeps === 'object' && rawDeps !== null
              ? (rawDeps as Record<string, string>)
              : undefined;

          if (!controller.signal.aborted) {
            setState({ status: 'ready', dependencies });
          }
        } else {
          if (!controller.signal.aborted) {
            setState({
              status: 'invalid',
              message:
                'The server returned an invalid or malformed response format.',
            });
          }
        }
      } else if (response.status === 503) {
        let dependencies: Record<string, string> | undefined;
        try {
          const body: unknown = await response.json();
          if (
            typeof body === 'object' &&
            body !== null &&
            typeof (body as ReadinessResponseBody).dependencies === 'object' &&
            (body as ReadinessResponseBody).dependencies !== null
          ) {
            dependencies = (body as ReadinessResponseBody)
              .dependencies as Record<string, string>;
          }
        } catch {
          // If body parsing fails on 503, dependencies remain undefined
        }

        if (!controller.signal.aborted) {
          setState({
            status: 'unavailable',
            message:
              'The server is currently unavailable or undergoing maintenance. Required system dependencies are not ready.',
            dependencies,
          });
        }
      } else {
        if (!controller.signal.aborted) {
          setState({
            status: 'invalid',
            message: 'The server returned an unexpected response status.',
          });
        }
      }
    } catch {
      if (controller.signal.aborted) {
        return;
      }
      setState({
        status: 'unavailable',
        message:
          'Unable to reach the server. Please check your network connection or verify that the server is online.',
      });
    }
  }, []);

  useEffect(() => {
    void checkReadiness();

    return () => {
      activeControllerRef.current?.abort();
    };
  }, [checkReadiness]);

  return { state, checkReadiness };
}
