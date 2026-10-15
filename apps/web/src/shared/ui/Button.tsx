import React, { forwardRef } from 'react';
import { Loader2 } from 'lucide-react';
import { cn } from './utils';

export type ButtonVariant =
  | 'primary'
  | 'secondary'
  | 'outline'
  | 'danger'
  | 'ghost';

export type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  loadingText?: string;
}

const variantStyles: Record<ButtonVariant, string> = {
  primary:
    'bg-brand-600 text-white hover:bg-brand-700 active:bg-brand-800 focus-visible:ring-brand-600',
  secondary:
    'bg-neutral-100 text-neutral-900 hover:bg-neutral-200 active:bg-neutral-300 focus-visible:ring-brand-600',
  outline:
    'border border-neutral-300 bg-white text-neutral-800 hover:bg-neutral-50 active:bg-neutral-100 focus-visible:ring-brand-600',
  danger:
    'bg-error-600 text-white hover:bg-error-700 active:bg-error-800 focus-visible:ring-error-600',
  ghost:
    'bg-transparent text-neutral-800 hover:bg-neutral-100 active:bg-neutral-200 focus-visible:ring-brand-600',
};

const sizeStyles: Record<ButtonSize, string> = {
  sm: 'px-2.5 py-1.5 text-xs font-medium gap-1.5',
  md: 'px-3.5 py-2 text-sm font-medium gap-2',
  lg: 'px-4.5 py-2.5 text-base font-medium gap-2.5',
};

const spinnerSizes: Record<ButtonSize, string> = {
  sm: 'h-3.5 w-3.5',
  md: 'h-4 w-4',
  lg: 'h-5 w-5',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      children,
      variant = 'primary',
      size = 'md',
      loading = false,
      loadingText,
      disabled = false,
      className,
      type = 'button',
      ...rest
    },
    ref
  ) => {
    const isDisabled = disabled || loading;

    return (
      <button
        ref={ref}
        type={type}
        disabled={isDisabled}
        aria-busy={loading ? 'true' : undefined}
        className={cn(
          'inline-flex items-center justify-center rounded-md transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
          'disabled:cursor-not-allowed disabled:opacity-60',
          variantStyles[variant],
          sizeStyles[size],
          className
        )}
        {...rest}
      >
        {loading && (
          <Loader2
            className={cn('animate-spin shrink-0', spinnerSizes[size])}
            aria-hidden="true"
          />
        )}
        <span>{loading && loadingText ? loadingText : children}</span>
      </button>
    );
  }
);

Button.displayName = 'Button';
