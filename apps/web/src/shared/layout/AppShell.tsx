import React, { useEffect, useRef, useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { Header } from '../components/Header';
import { SkipLink } from '../components/SkipLink';
import { Drawer } from '../ui/Drawer';
import { cn } from '../ui/utils';
import { Breadcrumbs, BreadcrumbItem } from './Breadcrumbs';
import {
  DEFAULT_NAVIGATION_ITEMS,
  NavigationItem,
  Session,
  visibleNavigation,
} from './navigation';

export interface AppShellProps {
  children: React.ReactNode;
  session?: Session;
  navigationItems?: readonly NavigationItem[];
  breadcrumbs?: BreadcrumbItem[];
}

interface NavigationListProps {
  items: NavigationItem[];
  onItemClick?: () => void;
}

function NavigationList({ items, onItemClick }: NavigationListProps) {
  return (
    <ul className="space-y-1 list-none p-0 m-0">
      {items.map((item) => {
        const Icon = item.icon;
        const isInteractive =
          item.status === 'implemented' && Boolean(item.path);

        return (
          <li key={item.id}>
            {isInteractive && item.path ? (
              <NavLink
                to={item.path}
                onClick={onItemClick}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-1',
                    isActive
                      ? 'bg-neutral-100 font-semibold text-neutral-900'
                      : 'text-neutral-600 hover:bg-neutral-50 hover:text-neutral-900'
                  )
                }
              >
                {Icon && (
                  <Icon
                    className="h-5 w-5 shrink-0 text-neutral-500"
                    aria-hidden="true"
                  />
                )}
                <span>{item.label}</span>
              </NavLink>
            ) : (
              <div
                aria-disabled="true"
                className="flex items-center justify-between rounded-md px-3 py-2 text-sm text-neutral-400 select-none cursor-not-allowed"
              >
                <div className="flex items-center gap-3">
                  {Icon && (
                    <Icon
                      className="h-5 w-5 shrink-0 text-neutral-400"
                      aria-hidden="true"
                    />
                  )}
                  <span className="font-medium text-neutral-500">
                    {item.label}
                  </span>
                </div>
                <span className="rounded bg-neutral-100 px-1.5 py-0.5 text-xs text-neutral-500">
                  {item.description ?? 'Not available yet'}
                </span>
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}

export function AppShell({
  children,
  session = { kind: 'anonymous' },
  navigationItems = DEFAULT_NAVIGATION_ITEMS,
  breadcrumbs,
}: AppShellProps) {
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const location = useLocation();
  const menuButtonRef = useRef<HTMLButtonElement | null>(null);
  const prevOpenRef = useRef(isMobileNavOpen);

  // Close mobile drawer on route change
  useEffect(() => {
    setIsMobileNavOpen(false);
  }, [location.pathname]);

  // Ensure focus returns to the menu button when the drawer closes
  useEffect(() => {
    if (prevOpenRef.current && !isMobileNavOpen) {
      menuButtonRef.current?.focus();
    }
    prevOpenRef.current = isMobileNavOpen;
  }, [isMobileNavOpen]);

  const visibleItems = visibleNavigation(navigationItems, session);

  return (
    <div className="flex min-h-screen flex-col bg-neutral-50 text-neutral-900">
      <SkipLink />
      <Header
        menuButtonRef={menuButtonRef}
        isMobileNavOpen={isMobileNavOpen}
        onMobileNavToggle={() => setIsMobileNavOpen((prev) => !prev)}
        session={session}
      />

      <div className="flex flex-1">
        {/* Desktop Sidebar Navigation */}
        <aside
          aria-label="Sidebar Navigation"
          className="hidden md:flex md:w-64 md:shrink-0 md:flex-col border-r border-neutral-200 bg-white"
        >
          <div className="flex flex-1 flex-col justify-between overflow-y-auto p-4">
            <nav aria-label="Sidebar Navigation">
              <NavigationList items={visibleItems} />
            </nav>

            {/* Tenant/User section strictly rendered ONLY when authenticated */}
            {session.kind === 'authenticated' && (
              <div
                data-testid="sidebar-tenant-user"
                className="mt-6 border-t border-neutral-200 pt-4"
              >
                <div className="text-xs font-semibold uppercase tracking-wider text-neutral-500">
                  Tenant
                </div>
                <div className="text-sm font-medium text-neutral-900 truncate">
                  {session.tenantName}
                </div>
                <div className="mt-2 text-xs font-semibold uppercase tracking-wider text-neutral-500">
                  User
                </div>
                <div className="text-sm text-neutral-700 truncate">
                  {session.userDisplayName}
                </div>
              </div>
            )}
          </div>
        </aside>

        {/* Main Landmark */}
        <main
          id="main-content"
          tabIndex={-1}
          className="flex-1 px-4 py-8 focus:outline-none sm:px-6 lg:px-8"
        >
          <div className="mx-auto max-w-7xl space-y-6">
            <Breadcrumbs items={breadcrumbs} />
            {children}
          </div>
        </main>
      </div>

      {/* Mobile Navigation Drawer */}
      <Drawer
        id="mobile-nav-drawer"
        isOpen={isMobileNavOpen}
        onClose={() => setIsMobileNavOpen(false)}
        title="Navigation"
        description="Application navigation"
        position="left"
        maxWidth="sm"
        closeLabel="Close navigation drawer"
      >
        <div className="flex h-full flex-col justify-between">
          <nav aria-label="Mobile Navigation">
            <NavigationList
              items={visibleItems}
              onItemClick={() => setIsMobileNavOpen(false)}
            />
          </nav>

          {/* Tenant/User section strictly rendered ONLY when authenticated */}
          {session.kind === 'authenticated' && (
            <div
              data-testid="mobile-tenant-user"
              className="mt-6 border-t border-neutral-200 pt-4"
            >
              <div className="text-xs font-semibold uppercase tracking-wider text-neutral-500">
                Tenant
              </div>
              <div className="text-sm font-medium text-neutral-900 truncate">
                {session.tenantName}
              </div>
              <div className="mt-2 text-xs font-semibold uppercase tracking-wider text-neutral-500">
                User
              </div>
              <div className="text-sm text-neutral-700 truncate">
                {session.userDisplayName}
              </div>
            </div>
          )}
        </div>
      </Drawer>
    </div>
  );
}
