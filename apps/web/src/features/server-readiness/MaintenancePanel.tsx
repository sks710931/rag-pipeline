import { AlertTriangle } from 'lucide-react';
import { ErrorState } from '../../shared/ui';

export interface MaintenancePanelProps {
  title?: string;
  message: string;
  onRetry: () => void;
  headingLevel?: 'h1' | 'h2' | 'h3' | 'h4';
  dependencies?: Record<string, string>;
  className?: string;
}

export function MaintenancePanel({
  title = 'System Maintenance',
  message,
  onRetry,
  headingLevel = 'h2',
  dependencies,
  className,
}: MaintenancePanelProps) {
  return (
    <div className={className}>
      <ErrorState
        title={title}
        description={message}
        icon={AlertTriangle}
        headingLevel={headingLevel}
        autoFocusHeading
        action={{
          label: 'Retry',
          onClick: onRetry,
          variant: 'danger',
        }}
      />
      {dependencies && Object.keys(dependencies).length > 0 && (
        <div className="mt-4 rounded-md border border-neutral-200 bg-neutral-50 p-4 text-left">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-neutral-600">
            Dependency Status
          </h3>
          <ul className="mt-2 space-y-1 text-xs text-neutral-700">
            {Object.entries(dependencies).map(([name, status]) => (
              <li key={name} className="flex items-center justify-between">
                <span className="font-mono text-neutral-800 capitalize">
                  {name}
                </span>
                <span
                  className={
                    status === 'ready' || status === 'configured'
                      ? 'font-medium text-success-800'
                      : 'font-medium text-error-800'
                  }
                >
                  {status}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
