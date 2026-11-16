# Backend implementation plan

## 1. DataAccess layer checklist
The following elements should remain present and consistent in the DataAccess layer:

- `ApplicationUser` inherits from `IdentityUser` and is used by `AppDbContext`.
- `Customer` contains identity, contact, and country metadata.
- `Order` contains status, money, row versioning, and ordering metadata.
- `OrderLineItem` contains per-line product, quantity, and pricing data.
- `IdempotencyRecord` captures request idempotency state and payload hash.
- `ProcessedMessage` prevents duplicate consumer processing.

Each entity should have a dedicated EF Core configuration class and be registered through `ApplyConfigurationsFromAssembly`.

## 2. Required project conventions
- Namespace convention: `DataAccessLayer.Models`, `DataAccessLayer.Contexts`, and `DataAccessLayer.Configurations`.
- App startup should register `AppDbContext` with SQL Server.
- Identity should be enabled through `AddIdentity<ApplicationUser, IdentityRole>()` and `AddEntityFrameworkStores<AppDbContext>()`.
- Keep database concerns in the DataAccess project, not in the API project.

## 3. Service layer follow-up
After the data layer is stable, implement the service layer with:
- customer operations
- order creation and retrieval
- order status transitions
- SADC currency validation rules
- idempotency checks
- messaging publication after successful persistence

## 4. Validation and migration checks
Before merge or commit, confirm:
- all entities compile cleanly
- `AppDbContext` contains all required `DbSet`s
- EF configuration is split by entity
- SQL Server connection config is in `appsettings*.json`
- the project builds successfully with `dotnet build`

## 5. Future implementation sequence
1. Confirm the DataAccess layer model set and configs match the domain requirements.
2. Add service interfaces and implementations.
3. Add FluentValidation rules for request shape.
4. Add API endpoints and authorization policies.
5. Add messaging and worker flows.
6. Generate migrations and validate against SQL Server.
7. Run integration tests and verify the end-to-end order flow.

## 6. Commit gate
No backend feature branch should be committed without verifying:
- build passes
- data model matches the approved plan
- configuration files are scoped to the correct entity
- Identity and EF integration are still working together
