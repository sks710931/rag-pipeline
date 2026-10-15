import React from 'react';
import {
  Activity,
  FileText,
  Search as SearchIcon,
  ScanText,
} from 'lucide-react';

export type NavigationItemStatus = 'implemented' | 'planned';

export interface NavigationItem {
  id: string;
  label: string;
  path?: string;
  status: NavigationItemStatus;
  description?: string;
  icon?: React.ComponentType<{ className?: string }>;
  requiresAuth?: boolean;
}

export type Session =
  | { kind: 'anonymous' }
  | { kind: 'authenticated'; tenantName: string; userDisplayName: string };

/**
 * Default application sidebar navigation items.
 *
 * Implemented today: System status (/status) only.
 * Planned items (Documents, Search, OCR) are declared with status 'planned'
 * and must render as non-interactive text with a visible "Not available yet"
 * indicator (no buttons, no href, aria-disabled text, not focusable).
 */
export const DEFAULT_NAVIGATION_ITEMS: readonly NavigationItem[] = [
  {
    id: 'status',
    label: 'System Status',
    path: '/status',
    status: 'implemented',
    icon: Activity,
  },
  {
    id: 'documents',
    label: 'Documents',
    status: 'planned',
    description: 'Not available yet',
    icon: FileText,
  },
  {
    id: 'search',
    label: 'Search',
    status: 'planned',
    description: 'Not available yet',
    icon: SearchIcon,
  },
  {
    id: 'ocr',
    label: 'OCR',
    status: 'planned',
    description: 'Not available yet',
    icon: ScanText,
  },
] as const;

/**
 * Default header / public navigation items.
 *
 * Every interactive link in the header is derived from this model with explicit status.
 * Implemented today: Status (/status) only.
 * Unimplemented routes or temporary setup routes (e.g. /setup for owner provisioning)
 * are excluded from permanent navigation to prevent dead ends and maintain capability
 * truthfulness ("no permanent create-owner nav"). Any planned header links must declare
 * status 'planned' with a clear description and render non-interactively.
 */
export const DEFAULT_HEADER_NAVIGATION_ITEMS: readonly NavigationItem[] = [
  {
    id: 'status',
    label: 'Status',
    path: '/status',
    status: 'implemented',
  },
] as const;

/**
 * Returns navigation items visible for the provided session state.
 *
 * IMPORTANT SECURITY NOTICE:
 * Client visibility is NOT security enforcement.
 * Hiding navigation items or marking items as planned or restricted in the client-side
 * UI is strictly for user experience, capability truthfulness, and interface ergonomics.
 * It does NOT enforce backend security or replace authoritative server-side authentication,
 * authorization, and tenant isolation policies. All sensitive capabilities must be guarded
 * authoritatively by the API server.
 */
export function visibleNavigation(
  items: readonly NavigationItem[],
  session: Session
): NavigationItem[] {
  return items.filter((item) => {
    if (item.requiresAuth && session.kind === 'anonymous') {
      return false;
    }
    return true;
  });
}
