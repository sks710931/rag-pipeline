import { Link, NavLink } from 'react-router-dom';
import { cn } from '../ui/utils';

export function Header() {
  return (
    <header className="border-b border-neutral-200 bg-white shadow-sm">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6 lg:px-8">
        <div>
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
      </div>
    </header>
  );
}
