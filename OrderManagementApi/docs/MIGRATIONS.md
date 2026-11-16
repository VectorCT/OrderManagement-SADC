# Database migration plan

## Current status
The project currently includes an initial EF Core migration for the data model:

- `InitialCreate`
- model coverage for `Customer`, `Order`, `OrderLineItem`, `IdempotencyRecord`, `ProcessedMessage`, and `ApplicationUser`
- Identity configuration via `AppDbContext : IdentityDbContext<ApplicationUser>`
- configuration split across dedicated EF Core configuration classes

## Commands

```bash
# add a migration from the current model
cd OrderManagementApi

dotnet ef migrations add InitialCreate \
  --project DataAccessLayer/DataAccessLayer.csproj \
  --startup-project OrderManagementApi/OrderManagementApi.csproj \
  --output-dir Migrations

# apply migrations to local SQL Server
dotnet ef database update \
  --project DataAccessLayer/DataAccessLayer.csproj \
  --startup-project OrderManagementApi/OrderManagementApi.csproj

# generate SQL script for deployment
dotnet ef migrations script --idempotent \
  --project DataAccessLayer/DataAccessLayer.csproj \
  --startup-project OrderManagementApi/OrderManagementApi.csproj \
  -o migrations.sql

# rollback to a previous migration
dotnet ef database update <PreviousMigrationName> \
  --project DataAccessLayer/DataAccessLayer.csproj \
  --startup-project OrderManagementApi/OrderManagementApi.csproj
```

## Zero-downtime rollout strategy
The migration plan should follow additive-first deployment practices. We should add new tables or non-breaking columns first, deploy app code that reads the new schema but remains compatible with the old version, and only then enforce stronger constraints such as `NOT NULL`, uniqueness, or column type changes.

For this project, the first migration introduces the core model and indexes. Future schema evolution should follow the same pattern: add new columns or tables, backfill in batches, validate the application writes both old and new shapes, and only then lock the schema down. This avoids breaking readers or writers during a mixed-version deployment.

## Rollback strategy
Each migration should keep a working `Down()` implementation. For a migration that is additive and backward compatible, rollback is straightforward: run `dotnet ef database update <previous>`. For destructive changes, the safest plan is to take a fresh backup or database snapshot before applying, only perform the change during a controlled maintenance window, and document the restore steps clearly.

In this project, the preferred rollback path is always to move forward with a corrective migration when data integrity is preserved. Destructive operations such as column drops or type changes should be treated as operationally risky and planned with explicit backup and recovery steps.

## CI validation
For the pipeline, the migration check should run against an empty SQL Server container and then verify that the generated SQL script executes successfully. The usual pattern is:

1. restore dependencies
2. build the solution
3. run `dotnet ef migrations script --idempotent`
4. start SQL Server in CI
5. apply the script to the empty database
6. fail the build if the script errors

## Operational notes
- Keep the `CustomerId, Status, CreatedAt` index and covering properties for listing queries.
- Preserve `HasPrecision(18,2)` for money values.
- Keep the `Order.RowVersion` optimistic concurrency guard in place.
- Treat the status transition workflow as idempotent at the service layer, using `IdempotencyRecord` to guard expensive writes.
