import { SystemHealth } from '../features/system-health/SystemHealth';
import { usePageMetadata } from '../shared/hooks/usePageMetadata';

export function StatusPage() {
  const { headingRef } = usePageMetadata('System Status');

  return (
    <div className="space-y-6">
      <div className="border-b border-neutral-200 pb-4">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-2xl font-bold tracking-tight text-neutral-900 focus:outline-none"
        >
          System Status
        </h2>
        <p className="mt-1 text-sm text-neutral-600">
          Real-time backend service health probe.
        </p>
      </div>

      <div className="max-w-xl">
        <SystemHealth />
      </div>
    </div>
  );
}
