# Copilot instructions for this repository

## Project intent
This repository is an ASP.NET Core 8 order-management backend for the SADC region. The implementation should stay aligned with the backend technical plan and preserve a clean separation of concerns.

## Architectural boundaries
- Keep the API layer thin: controllers should validate inputs and delegate to services.
- Keep business rules in the Services layer, not in entity classes.
- Keep EF Core configuration and entity mapping in DataAccessLayer.
- Keep DTOs and request/response contracts separate from persistence models.
- Avoid circular dependencies: Api -> Services -> DataAccess; Services -> Messaging; Worker -> Services + Messaging + DataAccess.

## DataAccessLayer requirements
- Use `DataAccessLayer.Models` for persistence entities.
- Use `DataAccessLayer.Contexts.AppDbContext` as the main EF Core context.
- Extend `IdentityDbContext<ApplicationUser>` for authentication and user management.
- Use one `IEntityTypeConfiguration<T>` per entity class.
- Apply configurations via `builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);`.
- Keep nullable and currency validations aligned with the domain rules in the technical plan.
- Name tables and indexes consistently with the current convention, for example: `Customers`, `Orders`, `OrderLineItems`, `IdempotencyRecords`, `ProcessedMessages`.

## Required domain model coverage
The DataAccess layer should continue to include:
- `Customer`
- `Order`
- `OrderLineItem`
- `IdempotencyRecord`
- `ProcessedMessage`
- `ApplicationUser`

The `Order` entity should retain:
- `CustomerId` and navigation property
- `Status`
- `CreatedAt`
- `CurrencyCode`
- `TotalAmount`
- `LineItems`
- `RowVersion`
- `Notes`
- `UpdatedAt`

## Configuration conventions
- Use Fluent API in the dedicated config files under `DataAccessLayer/Configurations`.
- Do not keep all configuration logic in one monolithic file.
- Use `HasPrecision(18,2)` for money values.
- Use `HasConversion<string>()` for enum storage when readability is preferred in SQL.
- Use `IsRowVersion()` on `Order.RowVersion`.
- Keep `Order -> Customer` FK delete behavior as `Restrict`.
- Keep `OrderLineItem -> Order` FK delete behavior as `Cascade`.

## Implementation guidance for future work
- Prefer `AsNoTracking()` for read-only query paths.
- Use `CancellationToken` in service and repository methods.
- Add validation rules for request shape using FluentValidation.
- Keep cross-entity checks such as country/currency validation in the service layer.
- Treat `Idempotency-Key` handling as an explicit requirement for status transitions.
- Keep optimistic concurrency and DB-level constraints in place.

## Commit and review checklist
Before committing backend changes, verify:
- The model set still matches the approved domain design.
- Every entity has a matching configuration class.
- `AppDbContext` includes all required `DbSet`s.
- EF Core and ASP.NET Identity dependencies compile cleanly.
- The build passes before commit.

## Required references
Use the backend plan and the current project structure as the source of truth:
- `BACKEND_PLAN.md` (project root or project documentation)
- `DataAccessLayer/Models/*`
- `DataAccessLayer/Configurations/*`
- `DataAccessLayer/Contexts/AppDbContext.cs`
