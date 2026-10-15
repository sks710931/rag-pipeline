import { Lock } from 'lucide-react';
import { useServerReadiness } from '../features/server-readiness/useServerReadiness';
import { MaintenancePanel } from '../features/server-readiness/MaintenancePanel';
import { usePageMetadata } from '../shared/hooks/usePageMetadata';
import { EmptyState, Skeleton, StatusBadge } from '../shared/ui';

export function LoginPage() {
  const { headingRef } = usePageMetadata('Sign In');
  const { state, checkReadiness } = useServerReadiness();

  return (
    <div className="space-y-6">
      <div className="border-b border-neutral-200 pb-4">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-2xl font-bold tracking-tight text-neutral-900 focus:outline-none"
        >
          Sign In
        </h2>
        <p className="mt-1 text-sm text-neutral-600">
          Authentication and user session management.
        </p>
      </div>

      {state.status === 'loading' && (
        <div
          role="status"
          aria-live="polite"
          aria-busy="true"
          className="space-y-4 rounded-lg border border-neutral-200 bg-white p-6 shadow-sm"
        >
          <div className="flex items-center space-x-3">
            <Skeleton
              height="1.5rem"
              width="10rem"
              loadingText="Checking server readiness..."
            />
          </div>
          <Skeleton height="4rem" width="100%" />
        </div>
      )}

      {(state.status === 'unavailable' || state.status === 'invalid') && (
        <MaintenancePanel
          title="System Maintenance"
          message={state.message}
          onRetry={() => void checkReadiness()}
          dependencies={
            state.status === 'unavailable' ? state.dependencies : undefined
          }
        />
      )}

      {state.status === 'ready' && (
        <div className="space-y-6">
          <EmptyState
            headingLevel="h3"
            icon={Lock}
            title="Sign-In Not Yet Available"
            description="Sign-in functionality is not yet available in this release. The server is ready and operational."
          />
          <div className="rounded-lg border border-neutral-200 bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-medium text-neutral-900">
                  Backend Status
                </h3>
                <p className="text-xs text-neutral-500">
                  Authoritative readiness probe reported 200 OK
                </p>
              </div>
              <StatusBadge severity="success">Ready</StatusBadge>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
