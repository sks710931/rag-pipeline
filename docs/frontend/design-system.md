# Pixel RAG Design System & Accessible UI Primitives

This document specifies the design tokens, reusable accessible UI primitives, keyboard behaviors, and UX conventions for Pixel RAG web applications (`apps/web`), implemented for GitHub Issue #6 (Plan Card `S0-PR02` in `docs/S0-Implementation-Plan.md`).

---

## 1. Design Tokens & Theme Policy

### 1.1 Theme Policy: Light Theme Only
Per the S0 design requirements, **only the Light theme is supported**. Dark theme is intentionally not implemented. All CSS variables and Tailwind utilities default to high-contrast light backgrounds.

### 1.2 Typography Scale
Font families:
- **Sans**: `Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`
- **Mono**: `ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace`

Scale:
- `text-xs`: `0.75rem` / `1rem` (12px / 16px)
- `text-sm`: `0.875rem` / `1.25rem` (14px / 20px)
- `text-base`: `1rem` / `1.5rem` (16px / 24px)
- `text-lg`: `1.125rem` / `1.75rem` (18px / 28px)
- `text-xl`: `1.25rem` / `1.75rem` (20px / 28px)
- `text-2xl`: `1.5rem` / `2rem` (24px / 32px)
- `text-3xl`: `1.875rem` / `2.25rem` (30px / 36px)
- `text-4xl`: `2.25rem` / `2.5rem` (36px / 40px)

### 1.3 Spacing Grid
Spacing adheres to a strict 4px base increment:
- `0.5`: `2px` (0.125rem)
- `1`: `4px` (0.25rem)
- `1.5`: `6px` (0.375rem)
- `2`: `8px` (0.5rem)
- `3`: `12px` (0.75rem)
- `4`: `16px` (1rem)
- `6`: `24px` (1.5rem)
- `8`: `32px` (2rem)
- `10`: `40px` (2.5rem)
- `12`: `48px` (3rem)
- `16`: `64px` (4rem)

### 1.4 Color Palettes & WCAG AA Contrast Compliance
All semantic colors are mapped as CSS custom properties (`--color-*`) in `src/index.css` and bound into `tailwind.config.js`:

| Token Group | Palette Base | Foreground Token | Background Token | Measured Contrast Ratio | WCAG AA Requirement |
|---|---|---|---|---|---|
| **Neutral** | Slate (`50`–`950`) | `--color-neutral-900` (`#0f172a`) | `--color-neutral-50` (`#f8fafc`) | 14.8:1 | Pass (>= 4.5:1) |
| **Brand** | Blue (`50`–`950`) | White (`#ffffff`) | `--color-brand-600` (`#2563eb`) | 4.6:1 | Pass (>= 4.5:1) |
| **Success** | Emerald (`50`–`950`) | `--color-success-800` (`#166534`) | `--color-success-50` (`#f0fdf4`) | 7.2:1 | Pass (>= 4.5:1) |
| **Warning** | Amber (`50`–`950`) | `--color-warning-900` (`#78350f`) | `--color-warning-50` (`#fffbeb`) | 8.5:1 | Pass (>= 4.5:1) |
| **Error** | Red (`50`–`950`) | `--color-error-800` (`#991b1b`) | `--color-error-50` (`#fef2f2`) | 7.4:1 | Pass (>= 4.5:1) |
| **Error Button** | Red | White (`#ffffff`) | `--color-error-600` (`#dc2626`) | 4.6:1 | Pass (>= 4.5:1) |
| **Info** | Sky (`50`–`950`) | `--color-info-800` (`#075985`) | `--color-info-50` (`#f0f9ff`) | 7.2:1 | Pass (>= 4.5:1) |

### 1.5 Focus Ring Utility
Interactive components support visible focus via `.focus-ring` or Tailwind `focus-visible:ring-2 focus-visible:ring-brand-600`:
- **Color**: `--color-focus-ring: #2563eb;`
- **Contrast**: 4.6:1 against `#ffffff` and 4.4:1 against `#f8fafc` (exceeds the 3:1 non-text contrast requirement in WCAG 2.1 SC 1.4.11).
- **Width & Offset**: 2px width with a 2px offset (`focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2`).

### 1.6 Reduced-Motion Handling
In `src/index.css`, `@media (prefers-reduced-motion: reduce)` overrides animations and transitions to an imperceptible duration (`0.01ms`) and forces `scroll-behavior: auto`:
```css
@media (prefers-reduced-motion: reduce) {
  *,
  ::before,
  ::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

### 1.7 Elevation & Breakpoints
- **Elevation shadows**: `--shadow-modal` (`0 25px 50px -12px rgb(0 0 0 / 0.25)`), `--shadow-drawer` (`-4px 0 24px 0 rgb(0 0 0 / 0.15)`), plus Tailwind standard elevation (`sm`, `md`, `lg`, `xl`).
- **Breakpoints**: `xs` (`320px`), `sm` (`640px`), `md` (`768px`), `lg` (`1024px`), `xl` (`1280px`), `2xl` (`1536px`).

---

## 2. Accessible UI Primitives (`src/shared/ui/`)

All primitives are located in `src/shared/ui/` with explicit TypeScript prop interfaces. Decorative icons from `lucide-react` carry `aria-hidden="true"`.

### 2.1 `TextField`
- **Purpose**: Accessible form input for textual data.
- **Accessibility Attributes**:
  - `label`: Associated to input via `htmlFor={id}`.
  - `hint`: Associated via `aria-describedby={`${id}-hint`}`.
  - `error`: Rendered with `role="alert"`, marked invalid with `aria-invalid="true"`, and associated via `aria-describedby`.
  - Required marker: Rendered with `aria-hidden="true"`.
- **Keyboard Behavior**: Standard input keyboard interactions. Visible focus ring on focus-visible.

### 2.2 `PasswordField`
- **Purpose**: Input for sensitive credentials with show/hide password visibility toggle.
- **Accessibility Attributes**:
  - Visibility toggle button: `type="button"`, `aria-label="Show password"` / `aria-label="Hide password"`, and `aria-pressed={showPassword}`.
  - Decorative `Eye` / `EyeOff` icons include `aria-hidden="true"`.
- **Security Guarantee**: The raw password value is never logged to the console, telemetry, or debug strings.
- **Keyboard Behavior**: Enter and Space keys toggle visibility when the toggle button is focused without submitting enclosing forms.

### 2.3 `Button`
- **Purpose**: Primary interactive action trigger.
- **Variants**: `primary`, `secondary`, `outline`, `danger`, `ghost`.
- **Sizes**: `sm`, `md`, `lg`.
- **Accessibility Attributes**:
  - `loading`: Sets `aria-busy="true"`, disables click actions, renders a decorative spinner (`Loader2` with `aria-hidden="true"`), and displays `loadingText` or children.
  - `disabled`: Sets native `disabled` attribute and `cursor-not-allowed`.
- **Keyboard Behavior**: Activated via Space and Enter keys. Visible 2px focus ring on `:focus-visible`.

### 2.4 `StatusBadge` & `StatusMessage`
- **Purpose**: Status reporting and banner alerts.
- **Severities**: `success`, `warning`, `error`, `info`, `neutral`.
- **Accessibility Attributes**:
  - **Role by Severity**: `role="alert"` for error severity; `role="status"` for info, success, warning, and neutral severities.
  - **Never Colour-Only**: Color is never the sole carrier of status. Each badge includes a distinct decorative icon plus screen-reader accessible status prefix (`Success:`, `Warning:`, `Error:`, `Info:`).
  - `StatusMessage` provides an explicit title heading, message body, and optional action slot (e.g., Retry button).

### 2.5 `Modal`
- **Purpose**: Centered dialog for high-priority user tasks or confirmations.
- **Accessibility Attributes**:
  - `role="dialog"`, `aria-modal="true"`.
  - Labelled by heading via `aria-labelledby`.
  - Described by optional description via `aria-describedby`.
  - Close button with `aria-label="Close modal"`.
  - Body scroll locked while open.
- **Keyboard & Focus Handling**:
  - **Focus Trap**: Traps focus inside the dialog; `Tab` from the last focusable element wraps to the first focusable element; `Shift+Tab` from the first element wraps to the last.
  - **Escape Key Closes**: Pressing `Escape` calls `onClose`.
  - **Focus Restoration**: Automatically stores the element focused prior to opening and restores focus to it when closed or unmounted.

### 2.6 `Drawer`
- **Purpose**: Slide-in panel (right or left) for side-tasks, filters, or inspection.
- **Accessibility Attributes**:
  - Same accessible behaviors as `Modal`: `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, accessible close button, body scroll locking.
  - Full keyboard focus trap, Escape key closing, and focus restoration to the trigger element upon exit.

### 2.7 `Stepper`
- **Purpose**: Multi-step progress indicator for sequential workflows (e.g. bootstrap/installation).
- **Accessibility Attributes**:
  - Rendered as an ordered list (`<ol>`) wrapped in `<nav aria-label="Progress steps">`.
  - Active step carries `aria-current="step"`.
  - **Never Colour-Only**: Each step contains non-color indicators: completed steps render a checkmark icon with `(Completed)` text; the active step renders `(Current step)`; upcoming steps render `(Upcoming)`.
  - When `onStepClick` is supplied, steps are rendered as native `<button>` elements that are fully keyboard operable.

### 2.8 `Skeleton`
- **Purpose**: Content placeholder while background data loads.
- **Accessibility Attributes**:
  - Visual animated shimmer carries `aria-hidden="true"`.
  - Screen readers are provided an accessible loading announcement via an invisible `<span className="sr-only" role="status" aria-live="polite">{loadingText}</span>`.
  - Honors reduced-motion settings by disabling pulse animation.

### 2.9 `Toast` & `ToastRegion`
- **Purpose**: Non-blocking notifications.
- **Accessibility Attributes**:
  - Landmark container: `role="region"` with `aria-label="Notifications"`.
  - **Live Regions**: Error toasts announce assertively (`aria-live="assertive"`, `role="alert"`); info, success, and warning toasts announce politely (`aria-live="polite"`, `role="status"`).
  - Dismiss button: `aria-label="Dismiss notification"`.
  - **Pause on Hover & Focus**: The auto-dismiss countdown timer pauses when the user hovers over the toast with a pointer or moves keyboard focus inside the toast. The timer resumes with the remaining time only when pointer and focus leave.

### 2.10 `EmptyState` & `ErrorState`
- **Purpose**: Clear contextual screens for empty collections and unrecoverable or operational errors.
- **Accessibility Attributes**:
  - Semantic heading hierarchy (`h2` or `h3`).
  - Decorative icons carry `aria-hidden="true"`.
  - `ErrorState` carries `role="alert"` and optional formatted error code/details.
  - Action slots render keyboard-operable `Button` primitives.

---

## 3. Dev-Only Sample Composition & Layout Limitation Note

### 3.1 Product Route Policy
Dev-only living sample pages or kitchen sinks **must not ship as product routes**. No route for sample exploration is added to `App.tsx` or any routing table.

### 3.2 Vitest-Rendered Sample Composition
Instead, the design system provides `src/shared/ui/SampleComposition.tsx`, an in-memory React component composing all design tokens and primitives together. It is exercised under Vitest test suites:
- Validated with `vitest-axe` axe-core smoke checks (`expect(await axe(container)).toHaveNoViolations()`).
- Exercises modal and drawer trigger/dismissal flows and interactive steppers.

### 3.3 Visual Layout Limitation in JSDOM (320px–375px)
> [!NOTE]
> JSDOM is a virtual DOM environment that does not implement CSS layout engines, text glyph metric calculations, or box geometry (`getBoundingClientRect()` returns zeros). Therefore, automated assertions regarding visual horizontal overflow or text clipping cannot be validated in JSDOM tests.
> Responsive visual checks at mobile widths (320px–375px) and desktop widths (1440px) require manual review or headless browser visual regression testing (e.g. Playwright) in a full browser environment, and are not claimed as automated in JSDOM.

---

## 4. Verification Commands

To verify design tokens, primitives, accessibility checks, and code quality:

```bash
# In apps/web
pnpm typecheck      # Type-check with strict TypeScript
pnpm lint           # ESLint analysis
pnpm test           # Vitest unit tests and axe-core accessibility smoke checks
pnpm format:check   # Prettier format verification
pnpm build          # Vite production bundle build
```
