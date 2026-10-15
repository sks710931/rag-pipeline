import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  HelpCircle,
  Info,
  type LucideIcon,
} from 'lucide-react';
import { cn } from './utils';

export type StatusSeverity =
  | 'success'
  | 'warning'
  | 'error'
  | 'info'
  | 'neutral';

export interface StatusBadgeProps
  extends React.HTMLAttributes<HTMLSpanElement> {
  severity?: StatusSeverity;
  children: React.ReactNode;
  showIcon?: boolean;
}

const severityConfig: Record<
  StatusSeverity,
  {
    classes: string;
    icon: LucideIcon;
    labelPrefix: string;
    role: 'status' | 'alert';
  }
> = {
  success: {
    classes: 'bg-success-50 text-success-800 border-success-200',
    icon: CheckCircle2,
    labelPrefix: 'Success',
    role: 'status',
  },
  warning: {
    classes: 'bg-warning-50 text-warning-900 border-warning-200',
    icon: AlertTriangle,
    labelPrefix: 'Warning',
    role: 'status',
  },
  error: {
    classes: 'bg-error-50 text-error-800 border-error-200',
    icon: AlertCircle,
    labelPrefix: 'Error',
    role: 'alert',
  },
  info: {
    classes: 'bg-info-50 text-info-800 border-info-200',
    icon: Info,
    labelPrefix: 'Info',
    role: 'status',
  },
  neutral: {
    classes: 'bg-neutral-100 text-neutral-800 border-neutral-200',
    icon: HelpCircle,
    labelPrefix: 'Status',
    role: 'status',
  },
};

export function StatusBadge({
  severity = 'neutral',
  children,
  showIcon = true,
  className,
  role: roleProp,
  ...rest
}: StatusBadgeProps) {
  const config = severityConfig[severity];
  const IconComponent = config.icon;
  const role = roleProp ?? config.role;

  return (
    <span
      role={role}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold',
        config.classes,
        className
      )}
      {...rest}
    >
      {showIcon && (
        <IconComponent className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      )}
      <span className="sr-only">{config.labelPrefix}: </span>
      <span>{children}</span>
    </span>
  );
}
