import React from 'react';
import { cn } from '../ui/utils';

export interface PageLayoutProps {
  heading: string;
  description?: string;
  actions?: React.ReactNode;
  headingRef?: React.RefObject<HTMLHeadingElement | null>;
  children?: React.ReactNode;
  className?: string;
}

export function PageLayout({
  heading,
  description,
  actions,
  headingRef,
  children,
  className,
}: PageLayoutProps) {
  return (
    <div className={cn('space-y-6', className)}>
      <div className="border-b border-neutral-200 pb-4">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2
              ref={headingRef}
              tabIndex={-1}
              className="text-2xl font-bold tracking-tight text-neutral-900 focus:outline-none"
            >
              {heading}
            </h2>
            {description && (
              <p className="mt-1 text-sm text-neutral-600">{description}</p>
            )}
          </div>
          {actions && <div className="flex items-center gap-3">{actions}</div>}
        </div>
      </div>
      <div>{children}</div>
    </div>
  );
}
