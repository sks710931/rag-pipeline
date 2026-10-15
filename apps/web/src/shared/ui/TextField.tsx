import React, { forwardRef, useId } from 'react';
import { AlertCircle } from 'lucide-react';
import { cn } from './utils';

export interface TextFieldProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'> {
  label: string;
  hint?: string;
  error?: string;
  containerClassName?: string;
  inputClassName?: string;
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(
  (
    {
      id,
      label,
      hint,
      error,
      required,
      disabled,
      className,
      containerClassName,
      inputClassName,
      'aria-describedby': ariaDescribedByProp,
      ...rest
    },
    ref
  ) => {
    const generatedId = useId();
    const inputId = id ?? generatedId;
    const hintId = `${inputId}-hint`;
    const errorId = `${inputId}-error`;

    const describedByIds = [
      error ? errorId : undefined,
      hint ? hintId : undefined,
      ariaDescribedByProp,
    ]
      .filter(Boolean)
      .join(' ');

    return (
      <div className={cn('flex flex-col gap-1.5', containerClassName)}>
        <label
          htmlFor={inputId}
          className="text-sm font-medium text-neutral-800"
        >
          {label}
          {required && (
            <span className="ml-1 text-error-600" aria-hidden="true">
              *
            </span>
          )}
        </label>

        <div className="relative">
          <input
            ref={ref}
            id={inputId}
            disabled={disabled}
            required={required}
            aria-invalid={Boolean(error)}
            aria-describedby={describedByIds || undefined}
            className={cn(
              'block w-full rounded-md border px-3 py-2 text-sm text-neutral-900 placeholder:text-neutral-400',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
              'transition-colors disabled:cursor-not-allowed disabled:bg-neutral-100 disabled:text-neutral-500',
              error
                ? 'border-error-600 bg-white text-error-900 focus-visible:border-error-600 focus-visible:ring-error-600'
                : 'border-neutral-300 bg-white focus-visible:border-brand-600 focus-visible:ring-brand-600',
              inputClassName,
              className
            )}
            {...rest}
          />
        </div>

        {hint && (
          <p id={hintId} className="text-xs text-neutral-500">
            {hint}
          </p>
        )}

        {error && (
          <p
            id={errorId}
            role="alert"
            className="flex items-center gap-1 text-xs font-medium text-error-700"
          >
            <AlertCircle
              className="h-3.5 w-3.5 shrink-0 text-error-600"
              aria-hidden="true"
            />
            <span>{error}</span>
          </p>
        )}
      </div>
    );
  }
);

TextField.displayName = 'TextField';
