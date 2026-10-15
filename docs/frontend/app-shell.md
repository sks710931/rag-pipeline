# Responsive Application Shell & Truthful Navigation

This document defines the architecture, typed navigation model, accessibility contract, and design guidelines for the Pixel RAG responsive application shell (`AppShell`), implemented for GitHub Issue #8 (Plan Card `S0-PR08`).

---

## 1. Architectural Principles

### 1.1 Capability Truthfulness

A fundamental rule of Pixel RAG is **truthfulness in interface presentation**:

- Every interactive link or button in the navigation shell must map to an active, real route or function in the application.
- Capabilities that are planned for future milestones (e.g., Documents, Search, OCR) are declared with `status: 'planned'`.
- Planned items **must never** render as interactive elements: no `<a>` tags, no `<button>` tags, no `href` attributes, no click handlers, and no keyboard focus (`tabIndex`).
- Planned items render with visible "Not available yet" indicator badges and `aria-disabled="true"` to accurately communicate status to both sighted and assistive technology users.

### 1.2 Client Visibility Is Not Security Enforcement

The shell provides filtering for navigation items based on the user's session state via `visibleNavigation(items, session)`:

> [!IMPORTANT] > **Client visibility is NOT security enforcement.**
> Hiding navigation links or marking items as planned or restricted in the client-side UI is strictly for user experience, capability truthfulness, and interface ergonomics. It does **not** enforce backend security or replace authoritative server-side authentication, authorization, and tenant isolation policies. All sensitive capabilities must be guarded authoritatively by the API server.

### 1.3 Anonymous Session Truthfulness

Currently, there is no authentication, current-user, tenant, or capability endpoint deployed on the backend:

- The frontend does not invent or mock session state, mock tenants, mock users, or mock counters.
- The default session state in the application is `{ kind: 'anonymous' }`.
- When `session.kind === 'anonymous'`, the shell strictly suppresses all tenant identifiers, user display names, organization switchers, and user profile menus. No placeholder user text (e.g., "Default Tenant", "Guest User") is ever rendered.

---

## 2. Shell Architecture & Layout Primitives

The shell structure is defined in `src/shared/layout/AppShell.tsx`:

```
┌────────────────────────────────────────────────────────────────────────┐
│ Header (banner)                                                        │
│ [Mobile Menu Trigger]  Pixel RAG                               [Status]│
├───────────────────┬────────────────────────────────────────────────────┤
│ Desktop Sidebar   │ Main Content (main id="main-content")              │
│ (aside / nav)     │                                                    │
│                   │ Breadcrumbs (nav aria-label="Breadcrumb")          │
│ • System Status   │ Home > System Status                               │
│ • Documents [N/A] ├────────────────────────────────────────────────────┤
│ • Search    [N/A] │ PageLayout (heading, description, optional actions)│
│ • OCR       [N/A] │                                                    │
│                   │ [Page Content: Status / Login / Setup / etc.]      │
│ [Tenant/User Info]│                                                    │
│ (Authenticated    │                                                    │
│  Only)            │                                                    │
└───────────────────┴────────────────────────────────────────────────────┘
```

### 2.1 Component Roster

| Component     | File Path                            | Primary Role                                                                                 |
| ------------- | ------------------------------------ | -------------------------------------------------------------------------------------------- |
| `AppShell`    | `src/shared/layout/AppShell.tsx`     | Master responsive container managing landmarks, drawer lifecycle, and layout composition.    |
| `Header`      | `src/shared/components/Header.tsx`   | Top banner containing branding, mobile disclosure trigger button, and model-derived navigation links. |
| `Breadcrumbs` | `src/shared/layout/Breadcrumbs.tsx`  | Accessible breadcrumb navigation (`nav aria-label="Breadcrumb"`) with `aria-current="page"`. |
| `PageLayout`  | `src/shared/layout/PageLayout.tsx`   | Standardized header container for pages (heading, description, and actions slot).            |
| `Drawer`      | `src/shared/ui/Drawer.tsx`           | Accessible slide-over overlay reused for mobile navigation.                                  |
| `SkipLink`    | `src/shared/components/SkipLink.tsx` | First focusable DOM element, targeting `<main id="main-content">`.                           |

---

## 3. Typed Navigation Model

Defined in `src/shared/layout/navigation.ts`:

```typescript
export type NavigationItemStatus = "implemented" | "planned";

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
    | { kind: "anonymous" }
    | { kind: "authenticated"; tenantName: string; userDisplayName: string };
```

### 3.1 Initial Navigation Inventories

#### Sidebar Navigation (`DEFAULT_NAVIGATION_ITEMS`)

| ID          | Label         | Path      | Status        | Presentation                                                                  |
| ----------- | ------------- | --------- | ------------- | ----------------------------------------------------------------------------- |
| `status`    | System Status | `/status` | `implemented` | Interactive `<NavLink>` with active route highlight and icon.                 |
| `documents` | Documents     | —         | `planned`     | Non-interactive text with "Not available yet" badge (`aria-disabled="true"`). |
| `search`    | Search        | —         | `planned`     | Non-interactive text with "Not available yet" badge (`aria-disabled="true"`). |
| `ocr`       | OCR           | —         | `planned`     | Non-interactive text with "Not available yet" badge (`aria-disabled="true"`). |

#### Header Navigation (`DEFAULT_HEADER_NAVIGATION_ITEMS`)

| ID       | Label  | Path      | Status        | Presentation                                         |
| -------- | ------ | --------- | ------------- | ---------------------------------------------------- |
| `status` | Status | `/status` | `implemented` | Interactive `<NavLink>` with active route highlight. |

Every interactive link in both the sidebar and the header is derived from the navigation model with explicit status. Unimplemented routes or temporary setup routes (e.g. `/setup` for initial owner provisioning) are dropped from permanent navigation to prevent dead ends and preserve capability truthfulness ("no permanent create-owner nav"). Any planned header links must declare `status: 'planned'` with a clear description and render non-interactively without focusability or click handlers.

### 3.2 Navigation Filtering: `visibleNavigation`

```typescript
export function visibleNavigation(
    items: readonly NavigationItem[],
    session: Session,
): NavigationItem[] {
    return items.filter((item) => {
        if (item.requiresAuth && session.kind === "anonymous") {
            return false;
        }
        return true;
    });
}
```

---

## 4. Mobile Drawer & Disclosure Contract

On mobile viewports (`< 768px`), the desktop sidebar is hidden (`hidden md:flex`), and a disclosure trigger button appears in the header:

1. **Disclosure Button Attributes**:
    - `aria-expanded={isMobileNavOpen}`: Tracks open/closed state.
    - `aria-controls="mobile-nav-drawer"`: Associates the trigger with the drawer element.
    - `aria-label`: Dynamically switches between `"Open navigation menu"` and `"Close navigation menu"`.
2. **Drawer Lifecycle**:
    - Reuses the accessible `Drawer` component positioned on the left (`position="left"`, `maxWidth="sm"`).
    - Closes when:
        - The user clicks the close button (`X`).
        - The user presses the `Escape` key.
        - The user clicks the backdrop overlay.
        - The user clicks an active navigation link that changes the route (`useLocation().pathname`).
3. **Focus Restoration**:
    - When the drawer closes by any trigger (Escape, close button, overlay click, or route navigation), keyboard focus is immediately restored to the mobile menu trigger button.

---

## 5. Keyboard Navigation & Focus Order

The tab sequence through the application shell follows strict logical hierarchy:

1. **Skip Link**: `a[href="#main-content"]` (`Skip to main content`).
2. **Mobile Menu Trigger**: Button visible on mobile screens (skipped or hidden on desktop).
3. **Site Identity**: Home link (`/`) wrapping the application title.
4. **Header Navigation Links**: Implemented links only (`Status` `/status`). Any planned header items are non-interactive and skipped.
5. **Sidebar Navigation Links**: Implemented links only (`System Status` `/status`).
6. **Planned Items**: Completely skipped during Tab traversal because they lack `tabIndex` and are rendered as semantic `<div>` elements.
7. **Breadcrumb Links**: Intermediate breadcrumbs (`Home` `/`). The current page item is a non-interactive `<span>` and is skipped.
8. **Main Content**: Page-level buttons, form inputs, and interactive components.

---

## 6. Accessibility & Compliance (WCAG 2.1 AA)

1. **Unique Landmark Labels**:
   To satisfy `axe` rule `landmark-unique`, every `<nav>` landmark specifies a unique, descriptive label:
    - Header Navigation: `<nav aria-label="Main Navigation">`
    - Desktop Sidebar: `<nav aria-label="Sidebar Navigation">`
    - Mobile Nav Drawer: `<nav aria-label="Mobile Navigation">`
    - Breadcrumbs: `<nav aria-label="Breadcrumb">`
2. **Color Contrast**:
    - All text and icon colors meet or exceed WCAG AA contrast ratio of 4.5:1.
    - Disabled badges use `text-neutral-500` against `bg-neutral-100` (contrast ratio: 4.64:1).
3. **Screen Reader Semantics**:
    - Planned items include `aria-disabled="true"`.
    - Active navigation links automatically apply `aria-current="page"`.
    - The active breadcrumb item contains `aria-current="page"`.
    - Separator icons between breadcrumb nodes carry `aria-hidden="true"`.
4. **Automated Testing**:
    - Shell states (default, open drawer, authenticated session, PageLayout with actions) are audited via `vitest-axe` with zero permitted violations.
