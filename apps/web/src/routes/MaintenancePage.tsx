import { Link } from 'react-router-dom';
import { useServerReadiness } from '../features/server-readiness/useServerReadiness';
import { MaintenancePanel } from '../features/server-readiness/MaintenancePanel';
import { usePageMetadata } from '../shared/hooks/usePageMetadata';
import { Button, Skeleton, StatusMessage } from '../shared/ui';

export function MaintenancePage() {
  const { headingRef } = usePageMetadata('Maintenance');
  const { state, checkReadiness } = useServerReadiness();

  return (
    <div className="space-y-6">
      <div className="border-b border-neutral-200 pb-4">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-2xl font-bold tracking-tight text-neutral-900 focus:outline-none"
        >
          System Maintenance
        </h2>
        <p className="mt-1 text-sm text-neutral-600">
          System operational status and dependency diagnostics.
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
          title="Maintenance Mode Active"
          message={state.message}
          onRetry={() => void checkReadiness()}
          dependencies={
            state.status === 'unavailable' ? state.dependencies : undefined
          }
        />
      )}

      {state.status === 'ready' && (
        <div className="space-y-6">
          <StatusMessage
            severity="success"
            title="All Systems Operational"
            showIcon
          >
            <p>
              The server is ready and all required dependencies are operational.
            </p>
          </StatusMessage>
          <div className="flex items-center gap-4">
            <Link to="/login">
              <Button variant="primary">Proceed to Sign In</Button>
            </Link>
            <Button variant="outline" onClick={() => void checkReadiness()}>
              Re-check Status
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
