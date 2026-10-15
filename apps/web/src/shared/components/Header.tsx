import React from 'react';
import { Link, NavLink } from 'react-router-dom';
import { Menu } from 'lucide-react';
import { cn } from '../ui/utils';
import type { NavigationItem, Session } from '../layout/navigation';
import {
  DEFAULT_HEADER_NAVIGATION_ITEMS,
  visibleNavigation,
} from '../layout/navigation';

export interface HeaderProps {
  onMobileNavToggle?: () => void;
  isMobileNavOpen?: boolean;
  session?: Session;
  navigationItems?: readonly NavigationItem[];
  menuButtonRef?: React.RefObject<HTMLButtonElement | null>;
}

export function Header({
  onMobileNavToggle,
  isMobileNavOpen = false,
  session = { kind: 'anonymous' },
  navigationItems = DEFAULT_HEADER_NAVIGATION_ITEMS,
  menuButtonRef,
}: HeaderProps = {}) {
  const visibleItems = visibleNavigation(navigationItems, session);

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
            {visibleItems.map((item) => {
              const isInteractive =
                item.status === 'implemented' && Boolean(item.path);

              if (isInteractive && item.path) {
                return (
                  <NavLink
                    key={item.id}
                    to={item.path}
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
                    {item.label}
                  </NavLink>
                );
              }

              return (
                <div
                  key={item.id}
                  aria-disabled="true"
                  className="flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm text-neutral-400 select-none cursor-not-allowed"
                >
                  <span className="font-medium text-neutral-500">
                    {item.label}
                  </span>
                  <span className="rounded bg-neutral-100 px-1.5 py-0.5 text-xs text-neutral-500">
                    {item.description ?? 'Not available yet'}
                  </span>
                </div>
              );
            })}
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
