# Pixel RAG Web Routing & Public Route Shell

This document defines the client-side routing architecture, maintenance UX, state machine, and accessibility conventions for Pixel RAG web applications (`apps/web`), implemented for GitHub Issue #7 (Plan Card `S0-PR07`).

---

## 1. Architectural Philosophy: The Server Stays Authoritative

A core architectural tenet of Pixel RAG is that **the client never invents, mocks, or assumes backend authorization or deployment lifecycle states**:

1. **No Client-Invented State**: The frontend does not synthesize fake bootstrap or setup checks. Future setup workflows will consume real backend contracts (`GET /api/v1/bootstrap/status` arriving in a later PR).
2. **No Premature Forms or Navigation**: Initial owner creation forms, nav links, or premature administrative controls are strictly prohibited until the corresponding backend capabilities are established.
3. **Authoritative Health Probes**: Client route guards and readiness indicators depend solely on real backend endpoints:
   - `GET /api/v1/system/health/live`: Returns `200 OK {"status": "live"}` when the backend process is active.
   - `GET /api/v1/system/health/ready`: Returns `200 OK {"status": "ready", "dependencies": {...}}` when all critical dependencies (e.g., PostgreSQL database) are operational. When a dependency fails, it returns `503 Service Unavailable {"status": "not_ready", "dependencies": {...}}`.

---

## 2. Public Route Shell Inventory

The client application uses `react-router-dom` (pinned exact: `7.18.4`) wrapped in `BrowserRouter` for standard browser navigation, back/forward history support, and deep-linking.

| Path | Purpose | Behavior & States | Document Title |
|---|---|---|---|
| `/` | Root entry | Automatically redirects to `/login` via `<Navigate to="/login" replace />`. | `Sign In \| Pixel RAG` |
| `/login` | Authentication gateway | Neutral screen stating sign-in is not yet available in this release. Displays backend readiness badge. If server is not ready (`unavailable` or `invalid`), renders the accessible maintenance panel. | `Sign In \| Pixel RAG` |
| `/setup` | System provisioning | Neutral screen stating setup is not yet available in this release. Never renders owner creation inputs or links. When not ready, renders the maintenance panel. | `Setup \| Pixel RAG` |
| `/maintenance` | Operator diagnostics | Actionable screen providing server readiness evaluation, dependency breakdown, and manual re-check actions. When ready, provides a return link to `/login`. | `Maintenance \| Pixel RAG` |
| `/status` | System health probe | Dedicated route hosting the real-time liveness probe component (`SystemHealth`), linked directly from the header navigation. | `System Status \| Pixel RAG` |
| `*` (Catch-all) | 404 Not Found | Accessible empty state with a return home link (`/`), ensuring no dead navigation ends. | `Page Not Found \| Pixel RAG` |

---

## 3. Server Readiness Hook (`useServerReadiness`)

The `useServerReadiness` hook manages asynchronous communication with `GET /api/v1/system/health/ready`.

### 3.1 State Transitions

```
[Initial] ───> loading ───┬───> 200 OK (status: 'ready') ───────────> ready
                          ├───> 503 Service Unavailable ─────────────> unavailable
                          ├───> Network Rejection / Offline ─────────> unavailable
                          └───> Unexpected Status / Bad JSON ────────> invalid
```

- **`loading`**: Request in flight. The UI presents a `Skeleton` placeholder with `role="status"`, `aria-busy="true"`, and `aria-live="polite"`.
- **`ready`**: Authoritative probe confirmed dependencies are healthy. The UI displays the operational view with a `StatusBadge` (`severity="success"`).
- **`unavailable`**: HTTP 503 or network failure. The UI displays `MaintenancePanel` with `role="alert"`.
- **`invalid`**: Unexpected HTTP status (e.g., 500, 404) or malformed payload. The UI displays `MaintenancePanel` with `role="alert"`.

### 3.2 Cancellation & Retry

- **`AbortController` Lifecycle**: Each check allocates an `AbortController`. In-flight requests are automatically aborted on component unmount or prior to initiating a new check. Aborted requests do not update component state or produce console errors.
- **Manual Retry**: Returns a `checkReadiness` callback bound to user-initiated retry buttons. No automatic refetch on window focus is executed.

### 3.3 Message Sanitization

All user-visible error messages emitted in `unavailable` and `invalid` states are statically defined and strictly sanitized:
- No raw server exceptions (`err.message`), stack traces, file system paths, or internal connection strings are exposed.
- No backend URLs or hostnames are rendered in client alerts.

---

## 4. Accessibility & UX Standards

The route shell complies with WCAG 2.1 AA accessibility guidelines:

1. **Accessible Landmarks**:
   - Skip Link (`SkipLink`) positioned at the top of the DOM targeting `<main id="main-content" tabIndex={-1}>`.
   - Banner (`<header>`) containing the site identity and `<nav aria-label="Main Navigation">`.
   - Navigation links use `NavLink` with high-contrast active states and distinct focus rings (`focus-visible:ring-2 focus-visible:ring-brand-600`).
2. **Focus Management**:
   - On client-side route transitions, focus is automatically moved to the route's main heading (`<h2 tabIndex={-1}>`) using the `usePageMetadata` hook.
   - When entering maintenance mode (`unavailable` or `invalid`), focus is shifted directly to the maintenance alert heading within `MaintenancePanel`.
3. **Screen Reader Announcements**:
   - Loading states use `role="status"` with `aria-live="polite"` and `aria-busy="true"`.
   - Maintenance panels and system warnings use `role="alert"` for immediate assistive feedback.
4. **Automated Audits**:
   - Every route and state (`ready`, `unavailable`, network failure, malformed payload, 404) is verified with `vitest-axe` (`expect(results).toHaveNoViolations()`).

---

## 5. Backend SPA Fallback Verification

Client-side routes are supported in production by the backend ASP.NET Core host (`apps/api/RagPipeline.Api/SpaHostingExtensions.cs`):

1. **`UseSpaHosting()`**:
   - Serves static assets from `apps/web/dist` with immutable caching headers for hashed assets (`/assets/*`).
   - Serves `index.html` with `Cache-Control: no-cache` and strict security headers (`Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`).
2. **`MapFallback()`**:
   - For all non-API GET/HEAD requests accepting `text/html`, requests fall back to `index.html`.
   - Strictly excludes `/api/*` paths from SPA fallback (returning standard 404 for nonexistent API routes).
