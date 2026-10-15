import React from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  HelpCircle,
  Info,
  type LucideIcon,
} from 'lucide-react';
import { cn } from './utils';
import type { StatusSeverity } from './StatusBadge';

export interface StatusMessageProps
  extends React.HTMLAttributes<HTMLDivElement> {
  severity?: StatusSeverity;
  title?: string;
  action?: React.ReactNode;
  showIcon?: boolean;
}

const messageSeverityConfig: Record<
  StatusSeverity,
  {
    containerClasses: string;
    titleClasses: string;
    textClasses: string;
    icon: LucideIcon;
    defaultTitle: string;
    role: 'status' | 'alert';
  }
> = {
  success: {
    containerClasses: 'border-success-200 bg-success-50 text-success-900',
    titleClasses: 'text-success-900',
    textClasses: 'text-success-800',
    icon: CheckCircle2,
    defaultTitle: 'Success',
    role: 'status',
  },
  warning: {
    containerClasses: 'border-warning-200 bg-warning-50 text-warning-950',
    titleClasses: 'text-warning-950',
    textClasses: 'text-warning-900',
    icon: AlertTriangle,
    defaultTitle: 'Warning',
    role: 'status',
  },
  error: {
    containerClasses: 'border-error-200 bg-error-50 text-error-900',
    titleClasses: 'text-error-900',
    textClasses: 'text-error-800',
    icon: AlertCircle,
    defaultTitle: 'Error',
    role: 'alert',
  },
  info: {
    containerClasses: 'border-info-200 bg-info-50 text-info-900',
    titleClasses: 'text-info-900',
    textClasses: 'text-info-800',
    icon: Info,
    defaultTitle: 'Information',
    role: 'status',
  },
  neutral: {
    containerClasses: 'border-neutral-200 bg-neutral-50 text-neutral-900',
    titleClasses: 'text-neutral-900',
    textClasses: 'text-neutral-700',
    icon: HelpCircle,
    defaultTitle: 'Note',
    role: 'status',
  },
};

export function StatusMessage({
  severity = 'neutral',
  title,
  children,
  action,
  showIcon = true,
  className,
  role: roleProp,
  ...rest
}: StatusMessageProps) {
  const config = messageSeverityConfig[severity];
  const IconComponent = config.icon;
  const role = roleProp ?? config.role;
  const displayTitle = title ?? config.defaultTitle;

  return (
    <div
      role={role}
      className={cn(
        'rounded-md border p-4',
        config.containerClasses,
        className
      )}
      {...rest}
    >
      <div className="flex items-start gap-3">
        {showIcon && (
          <div className="mt-0.5 shrink-0">
            <IconComponent className="h-5 w-5" aria-hidden="true" />
          </div>
        )}
        <div className="flex-1">
          {displayTitle && (
            <h3 className={cn('text-sm font-semibold', config.titleClasses)}>
              {displayTitle}
            </h3>
          )}
          {children && (
            <div
              className={cn(
                'text-sm',
                displayTitle ? 'mt-1' : '',
                config.textClasses
              )}
            >
              {children}
            </div>
          )}
          {action && <div className="mt-3">{action}</div>}
        </div>
      </div>
    </div>
  );
}
