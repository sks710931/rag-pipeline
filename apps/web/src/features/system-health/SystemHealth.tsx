import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { Button, StatusMessage } from '../../shared/ui';

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
        className="rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="system-health-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          System Health
        </h2>
        <div
          role="status"
          aria-busy="true"
          aria-live="polite"
          className="mt-4 flex items-center space-x-3 text-neutral-600"
        >
          <div
            className="h-5 w-5 animate-spin rounded-full border-2 border-brand-600 border-t-transparent"
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
        className="rounded-lg border border-error-200 bg-white p-6 shadow-sm"
      >
        <h2
          id="system-health-heading"
          className="text-lg font-semibold text-neutral-900"
        >
          System Health
        </h2>
        <div className="mt-4">
          <StatusMessage
            severity="error"
            title="System Unreachable"
            action={
              <Button
                variant="danger"
                size="sm"
                onClick={() => void fetchHealth()}
              >
                Retry
              </Button>
            }
          >
            <p className="mt-1 text-sm text-error-700">{state.message}</p>
          </StatusMessage>
        </div>
      </section>
    );
  }

  return (
    <section
      aria-labelledby="system-health-heading"
      className="rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
    >
      <h2
        id="system-health-heading"
        className="text-lg font-semibold text-neutral-900"
      >
        System Health
      </h2>
      <div className="mt-4 rounded-md border border-success-200 bg-success-50 p-4 text-success-900">
        <div className="flex items-center space-x-2">
          <CheckCircle2
            className="h-4 w-4 shrink-0 text-success-600"
            aria-hidden="true"
          />
          <span className="text-sm font-medium">Backend status:</span>
          <span className="text-sm font-bold capitalize text-success-800">
            {state.liveStatus}
          </span>
        </div>
        <p className="mt-2 text-xs text-success-700">
          Backend process is responsive and reporting live state directly from
          /api/v1/system/health/live.
        </p>
      </div>
    </section>
  );
}
