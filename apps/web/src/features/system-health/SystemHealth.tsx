import { useCallback, useEffect, useState } from 'react';

export type HealthState =
  | { type: 'loading' }
  | { type: 'success'; liveStatus: string }
  | { type: 'error'; message: string };

interface HealthApiResponse {
  status: string;
}

function isHealthApiResponse(data: unknown): data is HealthApiResponse {
  return (
    typeof data === 'object' &&
    data !== null &&
    'status' in data &&
    typeof (data as Record<string, unknown>)['status'] === 'string'
  );
}

export function SystemHealth() {
  const [state, setState] = useState<HealthState>({ type: 'loading' });

  const fetchHealth = useCallback(async () => {
    setState({ type: 'loading' });
    try {
      const response = await fetch('/api/v1/system/health/live');
      if (!response.ok) {
        throw new Error(
          `Health check failed with status code ${response.status} (${response.statusText || 'Error'}).`
        );
      }

      const json: unknown = await response.json();
      if (!isHealthApiResponse(json)) {
        throw new Error('Health check returned an invalid response format.');
      }

      setState({ type: 'success', liveStatus: json.status });
    } catch (err: unknown) {
      const message =
        err instanceof Error
          ? err.message
          : 'Failed to connect to the backend health endpoint.';
      setState({ type: 'error', message });
    }
  }, []);

  useEffect(() => {
    void fetchHealth();
  }, [fetchHealth]);

  if (state.type === 'loading') {
    return (
      <section
        aria-labelledby="system-health-heading"
        className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="system-health-heading"
          className="text-lg font-semibold text-slate-900"
        >
          System Health
        </h2>
        <div
          role="status"
          aria-busy="true"
          aria-live="polite"
          className="mt-4 flex items-center space-x-3 text-slate-600"
        >
          <div
            className="h-5 w-5 animate-spin rounded-full border-2 border-blue-600 border-t-transparent"
            aria-hidden="true"
          />
          <span className="text-sm">Checking backend liveness...</span>
        </div>
      </section>
    );
  }

  if (state.type === 'error') {
    return (
      <section
        aria-labelledby="system-health-heading"
        className="rounded-lg border border-red-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="system-health-heading"
          className="text-lg font-semibold text-slate-900"
        >
          System Health
        </h2>
        <div
          role="alert"
          className="mt-4 rounded-md border border-red-200 bg-red-50 p-4 text-red-900"
        >
          <div className="flex items-start">
            <span
              className="mr-2 inline-block h-2 w-2 translate-y-1.5 rounded-full bg-red-600"
              aria-hidden="true"
            />
            <div>
              <p className="text-sm font-medium">System Unreachable</p>
              <p className="mt-1 text-sm text-red-700">{state.message}</p>
            </div>
          </div>
          <div className="mt-4">
            <button
              type="button"
              onClick={() => void fetchHealth()}
              className="inline-flex items-center rounded-md bg-red-600 px-3 py-1.5 text-sm font-semibold text-white shadow-sm hover:bg-red-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600"
            >
              Retry
            </button>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section
      aria-labelledby="system-health-heading"
      className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm"
    >
      <h2
        id="system-health-heading"
        className="text-lg font-semibold text-slate-900"
      >
        System Health
      </h2>
      <div className="mt-4 rounded-md border border-emerald-200 bg-emerald-50 p-4 text-emerald-900">
        <div className="flex items-center space-x-2">
          <span
            className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-600"
            aria-hidden="true"
          />
          <span className="text-sm font-medium">Backend status:</span>
          <span className="text-sm font-bold capitalize text-emerald-800">
            {state.liveStatus}
          </span>
        </div>
        <p className="mt-2 text-xs text-emerald-700">
          Backend process is responsive and reporting live state directly from
          /api/v1/system/health/live.
        </p>
      </div>
    </section>
  );
}
