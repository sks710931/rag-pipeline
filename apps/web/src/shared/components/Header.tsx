import React from 'react';
import { Link, NavLink } from 'react-router-dom';
import { Menu } from 'lucide-react';
import { cn } from '../ui/utils';
import type { Session } from '../layout/navigation';

export interface HeaderProps {
  onMobileNavToggle?: () => void;
  isMobileNavOpen?: boolean;
  session?: Session;
  menuButtonRef?: React.RefObject<HTMLButtonElement | null>;
}

export function Header({
  onMobileNavToggle,
  isMobileNavOpen = false,
  session = { kind: 'anonymous' },
  menuButtonRef,
}: HeaderProps = {}) {
  return (
    <header className="border-b border-neutral-200 bg-white shadow-sm">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
        <div className="flex items-center gap-3">
          {onMobileNavToggle && (
            <button
              ref={menuButtonRef}
              type="button"
              id="mobile-nav-trigger"
              aria-expanded={isMobileNavOpen}
              aria-controls="mobile-nav-drawer"
              aria-label={
                isMobileNavOpen
                  ? 'Close navigation menu'
                  : 'Open navigation menu'
              }
              onClick={onMobileNavToggle}
              className="rounded-md p-1.5 text-neutral-600 hover:bg-neutral-100 hover:text-neutral-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-1 md:hidden transition-colors"
            >
              <Menu className="h-6 w-6" aria-hidden="true" />
            </button>
          )}

          <Link
            to="/"
            className="group block rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2"
          >
            <h1 className="text-xl font-bold tracking-tight text-neutral-900 group-hover:text-brand-600">
              Pixel RAG
            </h1>
            <p className="text-xs text-neutral-500">
              Enterprise Multimodal RAG Platform
            </p>
          </Link>
        </div>

        <div className="flex items-center gap-4">
          <nav
            aria-label="Main Navigation"
            className="flex items-center space-x-1 sm:space-x-2"
          >
            <NavLink
              to="/login"
              className={({ isActive }) =>
                cn(
                  'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2',
                  isActive
                    ? 'bg-neutral-100 font-semibold text-neutral-900'
                    : 'text-neutral-600 hover:bg-neutral-50 hover:text-neutral-900'
                )
              }
            >
              Sign In
            </NavLink>

            <NavLink
              to="/setup"
              className={({ isActive }) =>
                cn(
                  'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2',
                  isActive
                    ? 'bg-neutral-100 font-semibold text-neutral-900'
                    : 'text-neutral-600 hover:bg-neutral-50 hover:text-neutral-900'
                )
              }
            >
              Setup
            </NavLink>

            <NavLink
              to="/status"
              className={({ isActive }) =>
                cn(
                  'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-2',
                  isActive
                    ? 'bg-neutral-100 font-semibold text-neutral-900'
                    : 'text-neutral-600 hover:bg-neutral-50 hover:text-neutral-900'
                )
              }
            >
              Status
            </NavLink>
          </nav>

          {session.kind === 'authenticated' && (
            <div
              data-testid="header-tenant-user"
              className="hidden lg:flex items-center gap-3 pl-4 border-l border-neutral-200"
            >
              <div className="text-right">
                <p className="text-xs font-semibold uppercase tracking-wider text-neutral-500">
                  {session.tenantName}
                </p>
                <p className="text-sm font-medium text-neutral-900">
                  {session.userDisplayName}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
