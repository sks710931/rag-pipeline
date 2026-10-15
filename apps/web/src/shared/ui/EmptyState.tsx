import React from 'react';
import { Inbox, type LucideIcon } from 'lucide-react';
import { Button, type ButtonVariant } from './Button';
import { cn } from './utils';

export interface EmptyStateAction {
  label: string;
  onClick: () => void;
  variant?: ButtonVariant;
}

export interface EmptyStateProps extends React.HTMLAttributes<HTMLDivElement> {
  title: string;
  description?: string;
  icon?: LucideIcon;
  action?: EmptyStateAction | React.ReactNode;
  headingLevel?: 'h2' | 'h3' | 'h4';
}

export function EmptyState({
  title,
  description,
  icon: Icon = Inbox,
  action,
  headingLevel: HeadingTag = 'h3',
  className,
  ...rest
}: EmptyStateProps) {
  const isActionObject =
    action &&
    typeof action === 'object' &&
    'label' in action &&
    'onClick' in action;

  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center rounded-lg border-2 border-dashed border-neutral-300 p-8 text-center',
        className
      )}
      {...rest}
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-neutral-100 text-neutral-500">
        <Icon className="h-6 w-6" aria-hidden="true" />
      </div>

      <HeadingTag className="mt-4 text-base font-semibold text-neutral-900">
        {title}
      </HeadingTag>

      {description && (
        <p className="mt-1.5 max-w-sm text-sm text-neutral-500">
          {description}
        </p>
      )}

      {action && (
        <div className="mt-6">
          {isActionObject ? (
            <Button
              variant={(action as EmptyStateAction).variant ?? 'primary'}
              size="sm"
              onClick={(action as EmptyStateAction).onClick}
            >
              {(action as EmptyStateAction).label}
            </Button>
          ) : (
            (action as React.ReactNode)
          )}
        </div>
      )}
    </div>
  );
}
