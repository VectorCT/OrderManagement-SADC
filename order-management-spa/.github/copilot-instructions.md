# Copilot instructions for the frontend

Follow the [frontend implementation plan](../docs/frontend-implementation-plan.md) and this checklist. Keep frontend-only code in this project.

## Architecture and API

- Use strict TypeScript, React Router for routes, TanStack Query for server state, React Hook Form/Zod for validated forms, and CSS Modules for feature styling.
- Keep API request/response types generated from the Development Swagger contract. Do not hand-maintain duplicate API models or silently weaken generated types.
- Treat the .NET API as authoritative for authorization, validation, currencies, order state transitions, and final totals. Client checks improve UX only.
- Reuse query-key factories and invalidate the narrowest appropriate cache after mutations. Preserve idempotency keys when retrying the same order-status intent.
- Normalize API failures explicitly and preserve Problem Details. Render loading, empty, retryable-error, 401, 403, 409, and 422 states intentionally.

## Security and accessibility

- Keep development JWTs in memory only. Never put tokens, credentials, or secrets in local/session storage, source files, or committed environment files.
- Use semantic HTML, explicit form labels and error associations, keyboard-operable controls, visible focus, and accessible live feedback. Do not use clickable non-button elements.
- Hide write controls for the read-only role, while relying on the API for actual authorization.

## Quality and scope

- Add focused Vitest/RTL/MSW tests for new behavior and run the relevant npm checks before completion.
- Keep changes scoped to the requested frontend work; do not edit backend behavior unless a separate request requires it.
- Defer Playwright and production MSAL unless the core MVP workflow is complete.
