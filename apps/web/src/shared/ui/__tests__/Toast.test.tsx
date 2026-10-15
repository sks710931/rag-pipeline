import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { axe } from 'vitest-axe';
import { ToastItem, ToastRegion, type ToastData } from '../Toast';

describe('Toast and ToastRegion', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders region landmark with accessible name', () => {
    const toasts: ToastData[] = [{ id: '1', title: 'Test Alert', duration: 0 }];

    render(<ToastRegion toasts={toasts} onDismiss={() => {}} />);

    const region = screen.getByRole('region', { name: 'Notifications' });
    expect(region).toBeInTheDocument();
  });

  it('renders polite aria-live and status role for info/success/warning toasts', () => {
    const toast: ToastData = {
      id: 'info-1',
      type: 'info',
      title: 'Info Toast',
      description: 'System info',
      duration: 0,
    };

    render(<ToastItem toast={toast} onDismiss={() => {}} />);

    const toastElement = screen.getByRole('status');
    expect(toastElement).toBeInTheDocument();
    expect(toastElement).toHaveAttribute('aria-live', 'polite');
    expect(screen.getByText('Info Toast')).toBeInTheDocument();
    expect(screen.getByText('System info')).toBeInTheDocument();
  });

  it('renders assertive aria-live and alert role for error toasts', () => {
    const toast: ToastData = {
      id: 'err-1',
      type: 'error',
      title: 'Failure',
      description: 'Operation failed',
      duration: 0,
    };

    render(<ToastItem toast={toast} onDismiss={() => {}} />);

    const alertElement = screen.getByRole('alert');
    expect(alertElement).toBeInTheDocument();
    expect(alertElement).toHaveAttribute('aria-live', 'assertive');
  });

  it('calls onDismiss when dismiss button is clicked', async () => {
    const handleDismiss = vi.fn();
    const toast: ToastData = {
      id: 'dismiss-1',
      title: 'Dismissable',
      duration: 0,
    };

    render(<ToastItem toast={toast} onDismiss={handleDismiss} />);

    const dismissBtn = screen.getByRole('button', {
      name: 'Dismiss notification',
    });
    fireEvent.click(dismissBtn);

    expect(handleDismiss).toHaveBeenCalledWith('dismiss-1');
  });

  it('auto-dismisses after duration expires', () => {
    const handleDismiss = vi.fn();
    const toast: ToastData = {
      id: 'auto-1',
      title: 'Auto dismiss',
      duration: 3000,
    };

    render(<ToastItem toast={toast} onDismiss={handleDismiss} />);

    expect(handleDismiss).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(2999);
    });
    expect(handleDismiss).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(2);
    });
    expect(handleDismiss).toHaveBeenCalledWith('auto-1');
  });

  it('pauses auto-dismiss timeout on hover and resumes on mouse leave', () => {
    const handleDismiss = vi.fn();
    const toast: ToastData = {
      id: 'pause-hover',
      title: 'Hover pause',
      duration: 3000,
    };

    render(<ToastItem toast={toast} onDismiss={handleDismiss} />);

    // Advance 1000ms
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    const toastElement = screen.getByRole('status');

    // Hover -> pause
    fireEvent.mouseEnter(toastElement);

    // Advance 5000ms while hovered -> should NOT dismiss
    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(handleDismiss).not.toHaveBeenCalled();

    // Mouse leave -> resumes with remaining 2000ms
    fireEvent.mouseLeave(toastElement);

    act(() => {
      vi.advanceTimersByTime(1999);
    });
    expect(handleDismiss).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(2);
    });
    expect(handleDismiss).toHaveBeenCalledWith('pause-hover');
  });

  it('pauses auto-dismiss timeout on focus and resumes on blur', () => {
    const handleDismiss = vi.fn();
    const toast: ToastData = {
      id: 'pause-focus',
      title: 'Focus pause',
      duration: 3000,
    };

    render(<ToastItem toast={toast} onDismiss={handleDismiss} />);

    const toastElement = screen.getByRole('status');
    const dismissBtn = screen.getByRole('button', {
      name: 'Dismiss notification',
    });

    // Advance 1000ms
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    // Focus on button inside toast
    fireEvent.focus(dismissBtn);

    // Advance 5000ms while focused -> should NOT dismiss
    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(handleDismiss).not.toHaveBeenCalled();

    // Blur outside toast
    fireEvent.blur(toastElement, { relatedTarget: document.body });

    act(() => {
      vi.advanceTimersByTime(2001);
    });
    expect(handleDismiss).toHaveBeenCalledWith('pause-focus');
  });

  it('passes axe accessibility smoke test', async () => {
    vi.useRealTimers();
    const toasts: ToastData[] = [
      { id: 'axe-1', type: 'info', title: 'Notice', duration: 0 },
      { id: 'axe-2', type: 'error', title: 'Error alert', duration: 0 },
    ];

    const { container } = render(
      <ToastRegion toasts={toasts} onDismiss={() => {}} />
    );

    expect(await axe(container)).toHaveNoViolations();
  });
});
