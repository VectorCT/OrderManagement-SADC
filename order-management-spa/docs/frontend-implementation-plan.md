# Frontend implementation plan

## Goal and scope

Build a minimal, type-safe, accessible React single-page application for customer management, order creation, and order details. The frontend consumes the existing ASP.NET Core API; the API remains authoritative for authorization, validation, currency rules, and order totals.

The initial delivery targets the approximately two-hour MVP described below. Keep the UI small and functional, and defer Playwright end-to-end coverage and production MSAL integration unless the core workflow is complete.

## Foundation and conventions

- React with strict TypeScript, Vite, and React Router v7.
- TanStack Query v5 for server state; component state and URL search parameters for local and shareable UI state.
- React Hook Form and Zod for forms and client-side validation.
- `openapi-typescript` and `openapi-fetch` for types generated from the API's Development Swagger document (`http://localhost:5145/swagger/v1/swagger.json`).
- CSS Modules for feature styling; avoid a runtime UI framework.
- Vitest, React Testing Library, and MSW v2 for focused component and request tests.
- ESLint with `typescript-eslint` and `jsx-a11y`, plus Prettier.
- Keep API, auth, shared components, and feature-specific code in distinct modules under `src/`.
- Store development auth tokens in memory only. Do not commit credentials, generated environment secrets, or a token to browser storage.

## Planned structure

```text
order-management-spa/
├─ src/
│  ├─ api/                 # generated schema, typed client, query client
│  ├─ auth/                # in-memory dev auth provider and route guard
│  ├─ components/          # accessible reusable UI
│  ├─ features/
│  │  ├─ customers/        # customer queries, list, form, schema
│  │  └─ orders/           # order queries, list, form, details, schema
│  ├─ lib/                 # SADC currency rules, money formatting, hooks
│  ├─ test/                # MSW handlers and provider-aware test helpers
│  ├─ App.tsx
│  ├─ main.tsx
│  └─ routes.tsx
├─ docs/
├─ e2e/                    # optional Playwright tests
├─ Dockerfile
└─ nginx.conf
```

## Implementation sequence

### 1. Foundation — 25 minutes

- Complete the Vite React/TypeScript scaffold and establish strict type-check, lint, formatting, and test scripts.
- Generate API types from the running Development Swagger endpoint; verify generated output is stable and tracked.
- Build the typed API client with bearer-token and correlation-ID middleware, GET `no-store` behavior, and typed Problem Details errors.
- Add the shared TanStack Query client, in-memory development auth/role switcher, route shell, and error boundary.
- Add provider-aware test setup and MSW defaults.

### 2. Customers — 25 minutes

- Implement `/customers` with URL-synced search, 300 ms debounce, pagination, loading/empty/error states, and retry.
- Implement customer creation with Zod validation and a SADC country selector.
- Invalidate customer queries and show accessible success feedback after creation.

### 3. Orders list and creation — 35 minutes

- Implement `/orders` with URL-driven customer, status, sort, page, and page-size filters.
- Implement order creation with customer lookup, country-based currency choices, and dynamic line items.
- Show an estimated subtotal clearly distinguished from the server-calculated total.
- Map API validation errors to form fields and navigate to the created order.

### 4. Order details — 25 minutes

- Implement `/orders/:id`, line-item details, status badges, and only valid state-machine actions.
- Require a native dialog confirmation for cancellation.
- Send an intent-scoped idempotency key and reuse it for retrying the same failed intent.
- Handle 409 conflicts with detail-query invalidation; render 422 Problem Details inline.
- Poll Pending and Paid orders every four seconds, stopping for terminal states.

### 5. Tests and container — 20 minutes

- Prioritize MSW-backed tests for order currency/line-item behavior and status actions; cover customer validation and list states.
- Add the multi-stage Node-to-Nginx image and SPA fallback; proxy `/api` to the backend service.
- Add the frontend CI job for clean install, lint, type-check, coverage, and production build.
- Add Playwright only if time remains.

## UX, accessibility, and resilience requirements

- Use explicit labels, `aria-invalid`, linked error descriptions, and focus the first invalid field after submission.
- Use semantic tables with captions and column headers; pair status colors with text.
- Expose loading and toast feedback to assistive technology (`aria-busy`, `role="status"`, and a polite live region).
- Use native buttons and `<dialog>`; provide keyboard support, Escape dismissal, and focus restoration for dialogs.
- Wrap route content in an error boundary with a recovery action.
- Hide write controls for the read-only role, while relying on the API for actual authorization.
- Clear in-memory auth and request re-authentication on 401; explain 403 responses without implying the request succeeded.

## Auth and production notes

- In development, obtain a JWT from the API's `/dev/token` endpoint using a Read, Write, or Admin role selection. Keep the token in React state only.
- Document a future `@azure/msal-react` integration (`PublicClientApplication` and `acquireTokenSilent`) in the frontend README; do not implement it in the MVP.

## Verification and completion gate

Before calling the frontend milestone complete:

1. Run `npm ci`, `npm run lint`, `npm run typecheck`, `npm test -- --coverage`, and `npm run build` from `order-management-spa/`.
2. Verify API type generation is reproducible and CI detects contract drift.
3. Check keyboard navigation, validation focus, dialog behavior, live feedback, and responsive layouts.
4. Confirm the application never persists the development token and that server-side authorization remains authoritative.
5. Build and smoke-test the container, including SPA routes and `/api` proxy behavior.

## Scope cuts

If delivery falls behind, defer Playwright and MSAL first, use a standard select limited to the first 100 customers, and keep styling to clean CSS Modules without custom animation. Do not cut API-side validation/authorization, idempotency on status changes, or accessibility basics.
