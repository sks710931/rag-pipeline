import React from 'react';
import {
  AlertOctagon,
  CheckCircle2,
  Clock,
  Inbox,
  KeyRound,
  Loader2,
  ShieldAlert,
  type LucideIcon,
} from 'lucide-react';
import { Button, type ButtonVariant } from './Button';
import { Skeleton } from './Skeleton';
import { cn } from './utils';

export type PlatformStateType =
  | 'error'
  | 'loading'
  | 'forbidden'
  | 'empty'
  | 'expired-session'
  | 'recovery-pending'
  | 'success';

export interface PlatformStateAction {
  label: string;
  onClick: () => void;
  variant?: ButtonVariant;
}

export interface PlatformStateProps
  extends React.HTMLAttributes<HTMLDivElement> {
  type: PlatformStateType;
  title?: string;
  description?: string;
  details?: string;
  icon?: LucideIcon;
  action?: PlatformStateAction | React.ReactNode;
  headingLevel?: 'h1' | 'h2' | 'h3' | 'h4';
  autoFocusHeading?: boolean;
}

interface StateConfig {
  defaultTitle: string;
  defaultDescription: string;
  icon: LucideIcon;
  role: 'alert' | 'status' | 'region';
  containerClasses: string;
  iconContainerClasses: string;
  titleClasses: string;
  descriptionClasses: string;
  detailsClasses: string;
  defaultButtonVariant: ButtonVariant;
}

const STATE_CONFIGS: Record<PlatformStateType, StateConfig> = {
  error: {
    defaultTitle: 'Unable to Complete Request',
    defaultDescription:
      'The server encountered an error while processing your request. Please try again or contact your deployment administrator.',
    icon: AlertOctagon,
    role: 'alert',
    containerClasses: 'border-error-200 bg-error-50/50',
    iconContainerClasses: 'bg-error-100 text-error-600',
    titleClasses: 'text-error-900',
    descriptionClasses: 'text-error-800',
    detailsClasses: 'border-error-200 text-error-800 bg-white',
    defaultButtonVariant: 'danger',
  },
  loading: {
    defaultTitle: 'Loading System Data...',
    defaultDescription:
      'Please wait while the platform retrieves authoritative information from the server.',
    icon: Loader2,
    role: 'status',
    containerClasses: 'border-neutral-200 bg-neutral-50/60',
    iconContainerClasses: 'bg-brand-50 text-brand-600',
    titleClasses: 'text-neutral-900',
    descriptionClasses: 'text-neutral-600',
    detailsClasses: 'border-neutral-200 text-neutral-700 bg-white',
    defaultButtonVariant: 'secondary',
  },
  forbidden: {
    defaultTitle: 'Access Denied',
    defaultDescription:
      'You do not have the required permissions to access this tenant resource or action. Contact your organization administrator to request access.',
    icon: ShieldAlert,
    role: 'alert',
    containerClasses: 'border-warning-200 bg-warning-50/50',
    iconContainerClasses: 'bg-warning-100 text-warning-800',
    titleClasses: 'text-warning-950',
    descriptionClasses: 'text-warning-900',
    detailsClasses: 'border-warning-200 text-warning-900 bg-white',
    defaultButtonVariant: 'secondary',
  },
  empty: {
    defaultTitle: 'No Records Found',
    defaultDescription:
      'There is no data available in this scope. Create or upload items to populate this view.',
    icon: Inbox,
    role: 'region',
    containerClasses:
      'border-2 border-dashed border-neutral-300 bg-neutral-50/30',
    iconContainerClasses: 'bg-neutral-100 text-neutral-600',
    titleClasses: 'text-neutral-900',
    descriptionClasses: 'text-neutral-600',
    detailsClasses: 'border-neutral-200 text-neutral-700 bg-white',
    defaultButtonVariant: 'primary',
  },
  'expired-session': {
    defaultTitle: 'Session Expired',
    defaultDescription:
      'Your authentication session has expired due to inactivity. Please sign in again to restore secure access.',
    icon: Clock,
    role: 'alert',
    containerClasses: 'border-warning-200 bg-warning-50/50',
    iconContainerClasses: 'bg-warning-100 text-warning-800',
    titleClasses: 'text-warning-950',
    descriptionClasses: 'text-warning-900',
    detailsClasses: 'border-warning-200 text-warning-900 bg-white',
    defaultButtonVariant: 'primary',
  },
  'recovery-pending': {
    defaultTitle: 'System Recovery Required',
    defaultDescription:
      'The cryptographic key protector is locked or uninitialized. Supply the offline recovery envelope or administrator passphrase to restore operations.',
    icon: KeyRound,
    role: 'alert',
    containerClasses: 'border-warning-300 bg-warning-50/70',
    iconContainerClasses: 'bg-warning-100 text-warning-900',
    titleClasses: 'text-warning-950',
    descriptionClasses: 'text-warning-900',
    detailsClasses: 'border-warning-300 text-warning-900 bg-white',
    defaultButtonVariant: 'primary',
  },
  success: {
    defaultTitle: 'Operation Successful',
    defaultDescription:
      'The requested platform operation has finished and changes have been durably recorded.',
    icon: CheckCircle2,
    role: 'status',
    containerClasses: 'border-success-200 bg-success-50/50',
    iconContainerClasses: 'bg-success-100 text-success-700',
    titleClasses: 'text-success-950',
    descriptionClasses: 'text-success-800',
    detailsClasses: 'border-success-200 text-success-800 bg-white',
    defaultButtonVariant: 'primary',
  },
};

export function PlatformState({
  type,
  title,
  description,
  details,
  icon,
  action,
  headingLevel: HeadingTag = 'h3',
  autoFocusHeading = false,
  className,
  ...rest
}: PlatformStateProps) {
  const headingRef = React.useRef<HTMLHeadingElement>(null);
  const config = STATE_CONFIGS[type];
  const Icon = icon ?? config.icon;

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
      role={config.role}
      aria-live={type === 'loading' ? 'polite' : undefined}
      aria-busy={type === 'loading' ? 'true' : undefined}
      className={cn(
        'flex flex-col items-center justify-center rounded-lg border p-8 text-center',
        config.containerClasses,
        className
      )}
      {...rest}
    >
      <div
        className={cn(
          'flex h-12 w-12 items-center justify-center rounded-full',
          config.iconContainerClasses
        )}
      >
        <Icon
          className={cn('h-6 w-6', type === 'loading' && 'animate-spin')}
          aria-hidden="true"
        />
      </div>

      <HeadingTag
        ref={headingRef}
        tabIndex={autoFocusHeading ? -1 : undefined}
        className={cn(
          'mt-4 text-base font-semibold focus:outline-none',
          config.titleClasses
        )}
      >
        {title ?? config.defaultTitle}
      </HeadingTag>

      <p className={cn('mt-1.5 max-w-md text-sm', config.descriptionClasses)}>
        {description ?? config.defaultDescription}
      </p>

      {type === 'loading' && (
        <div
          className="mt-6 w-full max-w-xs space-y-2"
          role="presentation"
          aria-hidden="true"
        >
          <Skeleton height="14px" width="100%" />
          <Skeleton height="14px" width="75%" className="mx-auto" />
        </div>
      )}

      {details && (
        <div
          className={cn(
            'mt-3 max-w-md overflow-x-auto rounded border p-2.5 text-left text-xs font-mono',
            config.detailsClasses
          )}
        >
          {details}
        </div>
      )}

      {action && (
        <div className="mt-6">
          {isActionObject ? (
            <Button
              variant={
                (action as PlatformStateAction).variant ??
                config.defaultButtonVariant
              }
              size="sm"
              onClick={(action as PlatformStateAction).onClick}
            >
              {(action as PlatformStateAction).label}
            </Button>
          ) : (
            (action as React.ReactNode)
          )}
        </div>
      )}
    </div>
  );
}
