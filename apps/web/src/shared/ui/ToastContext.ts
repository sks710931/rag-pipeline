import { createContext } from 'react';
import type { ToastData } from './Toast';

export interface ToastContextValue {
  toasts: ToastData[];
  toast: (toast: Omit<ToastData, 'id'>) => string;
  dismiss: (id: string) => void;
}

export const ToastContext = createContext<ToastContextValue | null>(null);
