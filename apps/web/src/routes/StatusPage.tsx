import { SystemHealth } from '../features/system-health/SystemHealth';
import { usePageMetadata } from '../shared/hooks/usePageMetadata';
import { PageLayout } from '../shared/layout/PageLayout';

export function StatusPage() {
  const { headingRef } = usePageMetadata('System Status');

  return (
    <PageLayout
      heading="System Status"
      description="Real-time backend service health probe."
      headingRef={headingRef}
    >
      <div className="max-w-xl">
        <SystemHealth />
      </div>
    </PageLayout>
  );
}
