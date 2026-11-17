---
applyTo: "order-management-spa/**"
---

# Frontend instructions

- Follow `order-management-spa/docs/frontend-implementation-plan.md` and `order-management-spa/README.md`; keep frontend-only work in `order-management-spa/` unless updating shared repository CI or documentation.
- Use strict TypeScript, React Router for routes, TanStack Query for server state, React Hook Form/Zod for validated forms, and CSS Modules for feature styling.
- Keep API request/response types generated from the Development Swagger contract. Do not hand-maintain duplicate API models or silently weaken generated types.
- Treat the .NET API as authoritative for authorization, validation, currencies, order state transitions, and final totals. Client checks improve UX only.
- Keep JWTs in memory only. Never put tokens, credentials, or secrets in local/session storage, source files, or committed environment files.
- Normalize API failures explicitly, preserve Problem Details, and render loading, empty, retryable-error, 401, 403, 409, and 422 states intentionally.
- Use semantic HTML, explicit form labels and error associations, keyboard-operable controls, visible focus, and accessible live feedback. Do not use clickable non-button elements.
- Reuse query-key factories and invalidate the narrowest appropriate cache after mutations. Preserve idempotency keys when retrying the same order-status intent.
- Add focused Vitest/RTL/MSW tests for new behavior and run the relevant npm checks before completion.
- Keep changes scoped to the requested frontend work; do not edit backend behavior unless a separate request requires it.
