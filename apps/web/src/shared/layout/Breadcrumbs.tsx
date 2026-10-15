import { Link, useLocation } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { cn } from '../ui/utils';

export interface BreadcrumbItem {
  label: string;
  path?: string;
}

export interface BreadcrumbsProps {
  items?: BreadcrumbItem[];
  className?: string;
}

const ROUTE_LABELS: Record<string, string> = {
  '/': 'Home',
  '/status': 'System Status',
  '/login': 'Sign In',
  '/setup': 'System Setup',
  '/maintenance': 'System Maintenance',
};

export function Breadcrumbs({ items, className }: BreadcrumbsProps) {
  const location = useLocation();

  const resolvedItems: BreadcrumbItem[] =
    items ??
    (() => {
      const currentPath = location.pathname;
      if (currentPath === '/' || currentPath === '') {
        return [{ label: 'Home' }];
      }

      const label = ROUTE_LABELS[currentPath] ?? 'Page Not Found';
      return [
        { label: 'Home', path: '/' },
        { label, path: currentPath },
      ];
    })();

  if (resolvedItems.length === 0) {
    return null;
  }

  return (
    <nav aria-label="Breadcrumb" className={cn('py-1', className)}>
      <ol className="flex items-center space-x-2 text-sm text-neutral-600">
        {resolvedItems.map((item, index) => {
          const isLast = index === resolvedItems.length - 1;

          return (
            <li
              key={`${item.label}-${index}`}
              className="flex items-center space-x-2"
            >
              {index > 0 && (
                <ChevronRight
                  className="h-4 w-4 shrink-0 text-neutral-500"
                  aria-hidden="true"
                />
              )}
              {isLast ? (
                <span
                  aria-current="page"
                  className="font-medium text-neutral-900"
                >
                  {item.label}
                </span>
              ) : item.path ? (
                <Link
                  to={item.path}
                  className="rounded hover:text-neutral-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600 focus-visible:ring-offset-1 transition-colors"
                >
                  {item.label}
                </Link>
              ) : (
                <span className="text-neutral-600">{item.label}</span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
