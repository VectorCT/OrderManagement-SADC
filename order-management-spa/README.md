# Order Management frontend

React and TypeScript single-page application for the OrderManagement-SADC API. The frontend plan and delivery checklist are in [`docs/frontend-implementation-plan.md`](docs/frontend-implementation-plan.md) and [`.github/IMPLEMENTATION-CHECKLIST.md`](.github/IMPLEMENTATION-CHECKLIST.md).

## Requirements

- Node.js 20.19+ or 22.12+
- npm
- The API running in Development at `http://localhost:5145` when generating the API contract or making live requests

## Local development

```powershell
npm ci
npm run dev
```

Vite serves the app on its development URL. Set `VITE_API_BASE_URL` when the API is hosted somewhere other than the same origin's `/api` path.

Generate API types from the backend's Development Swagger document:

```powershell
npm run gen:api
```

The API must be running at `http://localhost:5145`; generated types are written to `src/api/schema.d.ts`.

## Checks

```powershell
npm run lint
npm run typecheck
npm test -- --coverage
npm run build
```

See the implementation plan for architecture, workflow requirements, accessibility expectations, and scope cuts. The development JWT must remain in memory; do not persist it in browser storage.

## Container

Build and run the SPA behind Nginx:

```powershell
docker build -t order-management-spa .
docker run --rm -p 8081:80 --add-host=host.docker.internal:host-gateway order-management-spa
```

The image serves React Router paths and proxies `/api` to the backend service at `order-management-api:8080`.
