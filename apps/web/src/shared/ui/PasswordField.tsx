import React, { forwardRef, useId, useState } from 'react';
import { AlertCircle, Eye, EyeOff } from 'lucide-react';
import { cn } from './utils';

export interface PasswordFieldProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type' | 'size'> {
  label: string;
  hint?: string;
  error?: string;
  containerClassName?: string;
  inputClassName?: string;
}

export const PasswordField = forwardRef<HTMLInputElement, PasswordFieldProps>(
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
    const [showPassword, setShowPassword] = useState(false);
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

    const togglePasswordVisibility = () => {
      setShowPassword((prev) => !prev);
    };

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

        <div className="relative flex items-center">
          <input
            ref={ref}
            id={inputId}
            type={showPassword ? 'text' : 'password'}
            disabled={disabled}
            required={required}
            aria-invalid={Boolean(error)}
            aria-describedby={describedByIds || undefined}
            className={cn(
              'block w-full rounded-md border py-2 pl-3 pr-10 text-sm text-neutral-900 placeholder:text-neutral-500',
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

          <button
            type="button"
            disabled={disabled}
            onClick={togglePasswordVisibility}
            aria-label={showPassword ? 'Hide password' : 'Show password'}
            aria-pressed={showPassword}
            className={cn(
              'absolute right-1.5 flex h-7 w-7 items-center justify-center rounded text-neutral-500 hover:text-neutral-800',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-1',
              'disabled:cursor-not-allowed disabled:opacity-50'
            )}
          >
            {showPassword ? (
              <EyeOff className="h-4 w-4" aria-hidden="true" />
            ) : (
              <Eye className="h-4 w-4" aria-hidden="true" />
            )}
          </button>
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

PasswordField.displayName = 'PasswordField';
