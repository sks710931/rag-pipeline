import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  CheckCircle2,
  Info,
  X,
  type LucideIcon,
} from 'lucide-react';
import { cn } from './utils';
import { ToastContext } from './ToastContext';

export type ToastType = 'info' | 'success' | 'warning' | 'error';

export interface ToastData {
  id: string;
  type?: ToastType;
  title: string;
  description?: string;
  duration?: number; // duration in ms, default 5000 (0 for no auto-dismiss)
}

export interface ToastItemProps {
  toast: ToastData;
  onDismiss: (id: string) => void;
}

const toastTypeConfig: Record<
  ToastType,
  {
    icon: LucideIcon;
    containerClass: string;
    titleClass: string;
    textClass: string;
    role: 'alert' | 'status';
    ariaLive: 'assertive' | 'polite';
  }
> = {
  info: {
    icon: Info,
    containerClass: 'bg-white border-info-300 text-neutral-900',
    titleClass: 'text-info-900',
    textClass: 'text-neutral-600',
    role: 'status',
    ariaLive: 'polite',
  },
  success: {
    icon: CheckCircle2,
    containerClass: 'bg-white border-success-300 text-neutral-900',
    titleClass: 'text-success-900',
    textClass: 'text-neutral-600',
    role: 'status',
    ariaLive: 'polite',
  },
  warning: {
    icon: AlertTriangle,
    containerClass: 'bg-white border-warning-300 text-neutral-900',
    titleClass: 'text-warning-950',
    textClass: 'text-neutral-600',
    role: 'status',
    ariaLive: 'polite',
  },
  error: {
    icon: AlertCircle,
    containerClass: 'bg-white border-error-300 text-neutral-900',
    titleClass: 'text-error-900',
    textClass: 'text-neutral-600',
    role: 'alert',
    ariaLive: 'assertive',
  },
};

export function ToastItem({ toast, onDismiss }: ToastItemProps) {
  const { id, type = 'info', title, description, duration = 5000 } = toast;
  const config = toastTypeConfig[type];
  const Icon = config.icon;

  const [isPaused, setIsPaused] = useState(false);
  const remainingTimeRef = useRef<number>(duration);
  const startTimeRef = useRef<number>(Date.now());
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Handle timeout with pause / resume
  useEffect(() => {
    if (duration <= 0) return;

    if (!isPaused) {
      startTimeRef.current = Date.now();
      timerRef.current = setTimeout(() => {
        onDismiss(id);
      }, remainingTimeRef.current);
    } else {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
        const elapsed = Date.now() - startTimeRef.current;
        remainingTimeRef.current = Math.max(
          0,
          remainingTimeRef.current - elapsed
        );
      }
    }

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [id, duration, isPaused, onDismiss]);

  const handleMouseEnter = () => setIsPaused(true);
  const handleMouseLeave = () => setIsPaused(false);

  const handleFocus = () => setIsPaused(true);
  const handleBlur = (e: React.FocusEvent<HTMLDivElement>) => {
    if (!e.currentTarget.contains(e.relatedTarget as Node | null)) {
      setIsPaused(false);
    }
  };

  return (
    <div
      role={config.role}
      aria-live={config.ariaLive}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onFocus={handleFocus}
      onBlur={handleBlur}
      className={cn(
        'pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border p-4 shadow-lg transition-all',
        config.containerClass
      )}
    >
      <div className="mt-0.5 shrink-0">
        <Icon className="h-5 w-5" aria-hidden="true" />
      </div>

      <div className="flex-1">
        <p className={cn('text-sm font-semibold', config.titleClass)}>
          {title}
        </p>
        {description && (
          <p className={cn('mt-1 text-xs', config.textClass)}>{description}</p>
        )}
      </div>

      <button
        type="button"
        onClick={() => onDismiss(id)}
        aria-label="Dismiss notification"
        className={cn(
          'shrink-0 rounded p-1 text-neutral-500 hover:bg-neutral-100 hover:text-neutral-700',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-1',
          'transition-colors'
        )}
      >
        <X className="h-4 w-4" aria-hidden="true" />
      </button>
    </div>
  );
}

export interface ToastRegionProps {
  toasts: ToastData[];
  onDismiss: (id: string) => void;
  className?: string;
}

export function ToastRegion({
  toasts,
  onDismiss,
  className,
}: ToastRegionProps) {
  if (toasts.length === 0) return null;

  return (
    <div
      role="region"
      aria-label="Notifications"
      className={cn(
        'pointer-events-none fixed bottom-4 right-4 z-50 flex max-w-sm flex-col gap-2 p-4',
        className
      )}
    >
      {toasts.map((toast) => (
        <ToastItem key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  );
}

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastData[]>([]);

  const dismiss = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback((newToast: Omit<ToastData, 'id'>) => {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
    setToasts((prev) => [...prev, { ...newToast, id }]);
    return id;
  }, []);

  return (
    <ToastContext.Provider value={{ toasts, toast, dismiss }}>
      {children}
      <ToastRegion toasts={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}
