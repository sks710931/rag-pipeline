import { describe, expect, it } from 'vitest';
import {
  DEFAULT_HEADER_NAVIGATION_ITEMS,
  DEFAULT_NAVIGATION_ITEMS,
  NavigationItem,
  Session,
  visibleNavigation,
} from '../navigation';

describe('Navigation Model & Capability Truthfulness', () => {
  it('defines truthful default items where only System status is implemented today', () => {
    const statusItem = DEFAULT_NAVIGATION_ITEMS.find(
      (item) => item.id === 'status'
    );
    expect(statusItem).toBeDefined();
    expect(statusItem?.status).toBe('implemented');
    expect(statusItem?.path).toBe('/status');
    expect(statusItem?.label).toBe('System Status');

    // Planned items must not be marked implemented
    const plannedIds = ['documents', 'search', 'ocr'];
    for (const id of plannedIds) {
      const item = DEFAULT_NAVIGATION_ITEMS.find((i) => i.id === id);
      expect(item).toBeDefined();
      expect(item?.status).toBe('planned');
      expect(item?.description).toBe('Not available yet');
    }
  });

  it('defines truthful header items where only implemented capabilities are interactive', () => {
    const statusItem = DEFAULT_HEADER_NAVIGATION_ITEMS.find(
      (item) => item.id === 'status'
    );
    expect(statusItem).toBeDefined();
    expect(statusItem?.status).toBe('implemented');
    expect(statusItem?.path).toBe('/status');
    expect(statusItem?.label).toBe('Status');

    // Setup must NOT be a permanent navigation item (no permanent create-owner nav)
    expect(
      DEFAULT_HEADER_NAVIGATION_ITEMS.find((item) => item.id === 'setup')
    ).toBeUndefined();

    // Every interactive item in the header model must have an explicit path and implemented status
    for (const item of DEFAULT_HEADER_NAVIGATION_ITEMS) {
      if (item.status === 'implemented') {
        expect(item.path).toBeDefined();
        expect(item.path?.startsWith('/')).toBe(true);
      } else {
        expect(item.description).toBeDefined();
      }
    }
  });

  describe('visibleNavigation()', () => {
    const testItems: NavigationItem[] = [
      {
        id: 'status',
        label: 'System Status',
        path: '/status',
        status: 'implemented',
      },
      {
        id: 'documents',
        label: 'Documents',
        status: 'planned',
        description: 'Not available yet',
      },
      {
        id: 'admin',
        label: 'Admin Console',
        path: '/admin',
        status: 'implemented',
        requiresAuth: true,
      },
      {
        id: 'audit-logs',
        label: 'Audit Logs',
        status: 'planned',
        description: 'Not available yet',
        requiresAuth: true,
      },
    ];

    it('filters out items requiring authentication when session is anonymous', () => {
      const session: Session = { kind: 'anonymous' };
      const visible = visibleNavigation(testItems, session);

      const visibleIds = visible.map((i) => i.id);
      expect(visibleIds).toContain('status');
      expect(visibleIds).toContain('documents');
      expect(visibleIds).not.toContain('admin');
      expect(visibleIds).not.toContain('audit-logs');
    });

    it('retains items requiring authentication when session is authenticated', () => {
      const session: Session = {
        kind: 'authenticated',
        tenantName: 'Acme Health',
        userDisplayName: 'Dr. Jane Smith',
      };
      const visible = visibleNavigation(testItems, session);

      const visibleIds = visible.map((i) => i.id);
      expect(visibleIds).toContain('status');
      expect(visibleIds).toContain('documents');
      expect(visibleIds).toContain('admin');
      expect(visibleIds).toContain('audit-logs');
    });

    it('preserves all default navigation items for anonymous users as none require auth', () => {
      const session: Session = { kind: 'anonymous' };
      const visible = visibleNavigation(DEFAULT_NAVIGATION_ITEMS, session);

      expect(visible).toHaveLength(DEFAULT_NAVIGATION_ITEMS.length);
      expect(visible.map((i) => i.id)).toEqual(
        DEFAULT_NAVIGATION_ITEMS.map((i) => i.id)
      );
    });

    it('preserves all default header navigation items for anonymous users', () => {
      const session: Session = { kind: 'anonymous' };
      const visible = visibleNavigation(
        DEFAULT_HEADER_NAVIGATION_ITEMS,
        session
      );

      expect(visible).toHaveLength(DEFAULT_HEADER_NAVIGATION_ITEMS.length);
      expect(visible.map((i) => i.id)).toEqual(
        DEFAULT_HEADER_NAVIGATION_ITEMS.map((i) => i.id)
      );
    });
  });
});
