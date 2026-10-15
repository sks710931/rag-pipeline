import React from 'react';
import { AlertOctagon, type LucideIcon } from 'lucide-react';
import { Button, type ButtonVariant } from './Button';
import { cn } from './utils';

export interface ErrorStateAction {
  label: string;
  onClick: () => void;
  variant?: ButtonVariant;
}

export interface ErrorStateProps extends React.HTMLAttributes<HTMLDivElement> {
  title: string;
  description?: string;
  errorDetails?: string;
  icon?: LucideIcon;
  action?: ErrorStateAction | React.ReactNode;
  headingLevel?: 'h1' | 'h2' | 'h3' | 'h4';
  autoFocusHeading?: boolean;
}

export function ErrorState({
  title,
  description,
  errorDetails,
  icon: Icon = AlertOctagon,
  action,
  headingLevel: HeadingTag = 'h3',
  autoFocusHeading = false,
  className,
  ...rest
}: ErrorStateProps) {
  const headingRef = React.useRef<HTMLHeadingElement>(null);

  React.useEffect(() => {
    if (autoFocusHeading) {
      headingRef.current?.focus();
    }
  }, [autoFocusHeading]);

  const isActionObject =
    action &&
    typeof action === 'object' &&
    'label' in action &&
    'onClick' in action;

  return (
    <div
      role="alert"
      className={cn(
        'flex flex-col items-center justify-center rounded-lg border border-error-200 bg-error-50/50 p-8 text-center',
        className
      )}
      {...rest}
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-error-100 text-error-600">
        <Icon className="h-6 w-6" aria-hidden="true" />
      </div>

      <HeadingTag
        ref={headingRef}
        tabIndex={autoFocusHeading ? -1 : undefined}
        className="mt-4 text-base font-semibold text-error-900 focus:outline-none"
      >
        {title}
      </HeadingTag>

      {description && (
        <p className="mt-1.5 max-w-sm text-sm text-error-800">{description}</p>
      )}

      {errorDetails && (
        <div className="mt-3 max-w-md overflow-x-auto rounded border border-error-200 bg-white p-2 text-left text-xs font-mono text-error-800">
          {errorDetails}
        </div>
      )}

      {action && (
        <div className="mt-6">
          {isActionObject ? (
            <Button
              variant={(action as ErrorStateAction).variant ?? 'danger'}
              size="sm"
              onClick={(action as ErrorStateAction).onClick}
            >
              {(action as ErrorStateAction).label}
            </Button>
          ) : (
            (action as React.ReactNode)
          )}
        </div>
      )}
    </div>
  );
}
