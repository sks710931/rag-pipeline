import { Link } from 'react-router-dom';
import { FileQuestion } from 'lucide-react';
import { usePageMetadata } from '../shared/hooks/usePageMetadata';
import { Button, EmptyState } from '../shared/ui';

export function NotFoundPage() {
  const { headingRef } = usePageMetadata('Page Not Found');

  return (
    <div className="space-y-6">
      <div className="border-b border-neutral-200 pb-4">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-2xl font-bold tracking-tight text-neutral-900 focus:outline-none"
        >
          Page Not Found
        </h2>
        <p className="mt-1 text-sm text-neutral-600">
          The requested URL does not exist on this server.
        </p>
      </div>

      <EmptyState
        headingLevel="h3"
        icon={FileQuestion}
        title="404 — Not Found"
        description="The page you are looking for could not be found. Please check the URL or return to the application home."
        action={
          <Link to="/">
            <Button variant="primary">Return Home</Button>
          </Link>
        }
      />
    </div>
  );
}
