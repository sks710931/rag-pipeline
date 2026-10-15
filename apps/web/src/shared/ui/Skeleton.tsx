import React from 'react';
import { cn } from './utils';

export interface SkeletonProps extends React.HTMLAttributes<HTMLDivElement> {
  width?: string | number;
  height?: string | number;
  circle?: boolean;
  loadingText?: string;
}

export function Skeleton({
  width,
  height,
  circle = false,
  loadingText = 'Loading...',
  className,
  style,
  ...rest
}: SkeletonProps) {
  return (
    <div className="relative inline-block w-full" {...rest}>
      {/* Visually hidden loading status for screen readers */}
      <span className="sr-only" role="status" aria-live="polite">
        {loadingText}
      </span>

      {/* Decorative skeleton shape */}
      <div
        aria-hidden="true"
        className={cn(
          'animate-pulse bg-neutral-200',
          circle ? 'rounded-full' : 'rounded-md',
          className
        )}
        style={{
          width,
          height: height ?? (circle ? width : '1rem'),
          ...style,
        }}
      />
    </div>
  );
}
