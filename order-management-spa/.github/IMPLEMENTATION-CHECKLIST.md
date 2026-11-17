# Frontend implementation checklist

This checklist tracks the recommended implementation sequence in the [frontend implementation plan](../docs/frontend-implementation-plan.md). Update task statuses as work progresses.

## Bootstrap

- [x] Scaffolded the strict React/TypeScript Vite application in `order-management-spa/`.
- [x] Added the planned core dependencies, lint/test tooling, and a starter smoke test; clean install, lint, type-check, tests, and production build pass.

## Foundation

- [x] Add the application route structure, shared QueryClient, API client, and error boundary.
- [x] API types are generated from Development Swagger and contract drift is detectable.
- [x] The API client adds the in-memory bearer token and a per-request correlation ID, disables browser GET caching, and surfaces typed Problem Details.
- [x] Development auth supports Read, Write, and Admin roles without local/session storage.
- [x] Shared query defaults, route recovery, and provider-aware test helpers are configured.

## Customers

- [ ] Search is debounced and search/page/page size are URL-synced; page size is capped at 100.
- [ ] Loading, empty, retryable error, and populated states are covered.
- [ ] Customer creation validates name, email, and one of the 16 SADC countries.
- [ ] Successful creation invalidates customer queries and gives accessible feedback.

## Orders

- [ ] List filters and sorting are URL-driven; totals use the order currency and server response.
- [ ] Order creation filters currencies by the selected customer's country.
- [ ] Line items support add/remove, require at least one row, and validate quantity and unit price.
- [ ] Estimated totals are explicitly labeled as estimates; server totals are displayed as authoritative.
- [ ] API validation errors map to the relevant fields; 409 and 422 outcomes are visible.
- [ ] Details show customer, status, currency, dates, server totals, and semantic line-item tables.
- [ ] Only valid status transitions are offered; cancellation requires a native confirmation dialog.
- [ ] Idempotency keys are stable across retries for one intent and replaced after success or a changed intent.
- [ ] Pending/Paid detail polling refreshes at four-second intervals and stops at terminal status.

## Quality and delivery

- [ ] Labels, error descriptions, invalid state, focus management, live feedback, and keyboard behavior meet the plan.
- [ ] Status meaning is conveyed with text as well as color; tables have captions and scoped headers.
- [ ] Vitest/RTL/MSW cover customer validation/list states and order form/status-action behavior.
- [ ] `npm ci`, lint, type-check, tests with coverage, and production build pass.
- [ ] Frontend CI runs in `order-management-spa/`; API contract drift is checked.
- [ ] Container serves SPA routes and proxies `/api` to the backend.
- [ ] Secrets and tokens are not committed or persisted in browser storage.
- [ ] Playwright and production MSAL are treated as optional scope, not prerequisites for the MVP.
