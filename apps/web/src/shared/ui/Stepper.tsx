import { Check } from 'lucide-react';
import { cn } from './utils';

export type StepStatus = 'complete' | 'current' | 'upcoming';

export interface StepItem {
  id: string | number;
  title: string;
  description?: string;
  status?: StepStatus;
}

export interface StepperProps {
  steps: StepItem[];
  currentStepIndex?: number;
  onStepClick?: (stepIndex: number) => void;
  className?: string;
  'aria-label'?: string;
}

export function Stepper({
  steps,
  currentStepIndex,
  onStepClick,
  className,
  'aria-label': ariaLabel = 'Progress steps',
}: StepperProps) {
  return (
    <nav aria-label={ariaLabel} className={cn('w-full', className)}>
      <ol className="flex flex-col gap-4 sm:flex-row sm:items-start sm:gap-2">
        {steps.map((step, index) => {
          let status: StepStatus = step.status ?? 'upcoming';
          if (typeof currentStepIndex === 'number') {
            if (index < currentStepIndex) {
              status = 'complete';
            } else if (index === currentStepIndex) {
              status = 'current';
            } else {
              status = 'upcoming';
            }
          }

          const isCurrent = status === 'current';
          const isComplete = status === 'complete';
          const isUpcoming = status === 'upcoming';
          const isClickable = Boolean(onStepClick);

          const statusTextMap: Record<StepStatus, string> = {
            complete: 'Completed',
            current: 'Current step',
            upcoming: 'Upcoming',
          };

          const StepWrapper = isClickable ? 'button' : 'div';
          const wrapperProps = isClickable
            ? {
                type: 'button' as const,
                onClick: () => onStepClick?.(index),
                className: cn(
                  'flex w-full items-start gap-3 rounded-md p-2 text-left transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2',
                  'hover:bg-neutral-50'
                ),
              }
            : {
                className: 'flex w-full items-start gap-3 p-2 text-left',
              };

          return (
            <li
              key={step.id}
              className="relative flex-1"
              aria-current={isCurrent ? 'step' : undefined}
            >
              <StepWrapper {...wrapperProps}>
                <div
                  className={cn(
                    'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold',
                    isComplete && 'bg-brand-600 text-white',
                    isCurrent &&
                      'border-2 border-brand-600 bg-white text-brand-600',
                    isUpcoming &&
                      'border border-neutral-300 bg-neutral-100 text-neutral-500'
                  )}
                  aria-hidden="true"
                >
                  {isComplete ? (
                    <Check className="h-4 w-4" />
                  ) : (
                    <span>{index + 1}</span>
                  )}
                </div>

                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <span
                      className={cn(
                        'text-sm font-medium',
                        isCurrent && 'font-semibold text-brand-700',
                        isComplete && 'text-neutral-900',
                        isUpcoming && 'text-neutral-500'
                      )}
                    >
                      {step.title}
                    </span>
                    <span className="sr-only"> ({statusTextMap[status]})</span>
                  </div>

                  {step.description && (
                    <p className="mt-0.5 text-xs text-neutral-500">
                      {step.description}
                    </p>
                  )}
                </div>
              </StepWrapper>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
