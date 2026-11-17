# OrderManagement-SADC

An ASP.NET Core 8 and Entity Framework Core API for SADC-focused customer and order management. The solution includes customer, order, authentication, and role-management endpoints; EF Core persistence and migrations; request validation; and integration tests.

## Prerequisites

- .NET 10 SDK to read and operate on the checked-in `.slnx` solution (projects target .NET 8).
- Docker Desktop with Docker Compose v2 for the local SQL Server and RabbitMQ services.
- Git.

## Start local dependencies

From the repository root, create a private Compose environment file and replace the example values with strong local-only credentials:

```powershell
Copy-Item .env.example .env
# Edit .env before starting the services.
docker compose up -d
docker compose ps
```

Compose starts SQL Server 2022 Developer on `localhost:1433` and RabbitMQ on `localhost:5672`; its management UI is at `http://localhost:15672`. The broker user/password come from `.env`. Both services use named volumes, so their data persists across container restarts. Stop the services with `docker compose down`; add `-v` only when intentionally deleting their persisted data.

The Compose file supplies infrastructure only; it does not run the API or imply that RabbitMQ publishing/consuming or a background worker has been implemented. RabbitMQ is the selected broker for the separate messaging/worker work.

## Configure and run the API

The API reads `ConnectionStrings:DefaultConnection`. When using Compose SQL Server on Windows, configure a user secret so the connection string and password stay outside the repository. Use a SQL login with access to `OrderManagementDb` (for example, `orderManagement_user`) and replace the password placeholder below with that login's password. Use `Server=localhost,1433` when the API runs on the host, or `Server=sqlserver,1433` when it runs in the Compose network.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=OrderManagementDb;User Id=orderManagement_user;Password=<your-sql-login-password>;Encrypt=True;TrustServerCertificate=True" --project .\OrderManagementApi\OrderManagementApi\OrderManagementApi.csproj
```

If the EF command-line tool is not installed, install the matching major/minor tool:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.8
```

Apply the existing initial migration (from the repository root):

```powershell
dotnet ef database update --project .\OrderManagementApi\DataAccessLayer\DataAccessLayer.csproj --startup-project .\OrderManagementApi\OrderManagementApi\OrderManagementApi.csproj
```

Then start the API:

```powershell
dotnet run --project .\OrderManagementApi\OrderManagementApi\OrderManagementApi.csproj --launch-profile http
```

The current HTTP launch profile listens at `http://localhost:5145`. Swagger is enabled in the Development environment. Development data seeding runs when the API starts; register the configured admin email first if you want that account assigned the Admin role.

## Build and test

Run from the repository root:

```powershell
dotnet restore .\OrderManagementApi\OrderManagementApi.slnx
dotnet build .\OrderManagementApi\OrderManagementApi.slnx --configuration Release --no-restore
dotnet test .\OrderManagementApi\OrderManagementApi.slnx --configuration Release --no-build --no-restore
```

GitHub Actions runs these restore/build/test steps for pushes, pull requests, and manual dispatches. The test project includes API integration and request-validation tests.

## Configuration and implementation notes

- The main EF Core context is `DataAccessLayer.Contexts.AppDbContext`, backed by SQL Server and ASP.NET Core Identity.
- Persisted models include `Customer`, `Order`, `OrderLineItem`, `IdempotencyRecord`, `ProcessedMessage`, and `ApplicationUser`; EF mappings are in per-entity configuration files.
- Implemented service logic includes customer create/get/search, order create/get/list/status transition, order status transition rules, and SADC country/currency policy checks.
- Connection strings and development credentials should be supplied through user secrets or environment variables, never committed `appsettings` edits or `.env` files.
- `docker-compose.yml` and `.env.example` are for local infrastructure only; Compose requires the environment values and does not provide production deployment configuration.

See [`OrderManagementApi/docs/backend-implementation-plan.md`](OrderManagementApi/docs/backend-implementation-plan.md) for the backend plan and [`OrderManagementApi/docs/MIGRATIONS.md`](OrderManagementApi/docs/MIGRATIONS.md) for migration guidance.