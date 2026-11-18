# Technical Assessment Answers

These answers describe the current OrderManagement-SADC implementation and use its SQL Server schema and API behavior as examples. Where a question asks for a capability that is not fully implemented, that distinction is called out rather than implying it is already present.

## General questions

### 1. .NET/C#: async/await and `Task.Run`

In ASP.NET Core, I use `async`/`await` for I/O-bound operations and pass the request cancellation token through to the I/O API. This lets the request thread return to the pool while SQL Server, RabbitMQ, or another remote system is doing work. It does not make the underlying operation faster, but it improves server capacity under concurrent I/O. I avoid `.Result`, `.Wait()`, and `async void` in request paths because they can block threads or hide failures.

For example, `OrderService.CreateAsync` uses `FirstOrDefaultAsync` and `SaveChangesAsync`, each with `ct`; controller actions accept and forward `CancellationToken`. `Task.Run` is not appropriate around EF Core or RabbitMQ calls: it only queues the same I/O work to another thread. I would reserve it for bounded CPU-heavy work that cannot be made asynchronous, and would first consider whether that work belongs in a background worker instead of tying up an HTTP request.

### 2. ASP.NET Core: Minimal APIs vs controllers

Minimal APIs are concise and work well for small services or focused endpoints where route handlers, filters, and OpenAPI metadata are enough. Controllers provide a familiar structure for larger resource-oriented APIs, shared conventions, attributes, and action-level authorization/response metadata. Neither style is inherently faster in a meaningful way for this application.

This project uses controllers: `OrdersController` groups the order routes under `api/orders`, declares authorization policies per action, and documents response codes. Its create and status-transition actions reuse the validation helper in `ApiControllerBase`. That organization suits the current collection of customer, order, authentication, role, and diagnostics endpoints; I would choose minimal APIs for a small, isolated service rather than rewriting these controllers just for style.

### 3. EF Core: tracking, `AsNoTracking()`, and concurrency

Tracking is useful when an entity will be modified and saved in the same unit of work: EF Core keeps its original values and detects changes. For read-only endpoints, `AsNoTracking()` avoids that bookkeeping. This API uses it in customer reads/search, order reads/listing, customer lookup during order creation, and idempotency-record lookup. The status transition loads a tracked order because it changes its status and persists it.

The `Orders` table has a SQL Server `rowversion` column configured by `builder.Property(o => o.RowVersion).IsRowVersion()`. EF includes its original value in the update predicate; if another writer changed the row, the update affects zero rows and EF throws `DbUpdateConcurrencyException`. The worker explicitly handles that exception and checks whether another consumer already recorded the message. The API returns `RowVersion` in its order DTO, but its transition endpoint currently does not accept a client-supplied expected version or handle `DbUpdateConcurrencyException` separately; a production API should make that conflict explicit (HTTP 409) and return a safe current-state representation or ask the client to reload. Idempotency keys protect retries of the same request, but are not a replacement for optimistic concurrency.

### 4. Validation: DTO, domain, and database

I validate at multiple boundaries because each layer protects a different invariant:

- Request validation checks shape and user-input constraints. FluentValidation validators are registered from the assembly; `CreateOrderRequestValidator` and `CreateOrderLineItemRequestValidator` validate order requests before the service runs.
- The service/domain layer enforces business rules that require application context. `OrderService.CreateAsync` verifies that the customer exists, checks the customer's country/currency pairing through `ISadcCurrencyPolicy`, and computes `TotalAmount` from line items rather than trusting a client total. `OrderStateMachine` restricts status transitions.
- SQL Server constraints protect persisted integrity across all writers. The schema has foreign keys, unique indexes such as customer email and idempotency key/scope, required columns, and `decimal(18,2)` money fields.

FluentValidation is already used for request-level rules because the validators are easy to isolate and test. I would keep cross-entity rules out of DTO validation: for example, whether `USD` is valid for this particular customer's country requires the database lookup and currency policy. Database constraints remain the final guard against races and non-API writers.

### 5. Exception handling and error contracts

The API uses a global `ApiExceptionHandler` registered with ASP.NET Core's exception-handler middleware. It logs the exception, returns a generic message for unexpected failures, and maps EF `DbUpdateException` conflicts to HTTP 409. It deliberately does not send exception details to clients. Request binding/validation, authentication challenges, forbidden responses, and service failures also return the application's consistent `ApiResponse` envelope (`Code`, `Message`, `Results`, and `Endpoint`).

That envelope is the contract clients currently consume; it is not RFC 7807 Problem Details. If standard interoperability becomes a requirement, I would migrate errors consistently to `application/problem+json` with `type`, `title`, `status`, `detail`, and `instance`, and use a stable extension such as `errors` for field validation. I would keep internal stack traces and SQL details in structured server logs, not in `detail`. A key follow-up is mapping `DbUpdateConcurrencyException` to a specific 409 response instead of relying on the broader database-update mapping.

### 6. API design: search, pagination, sorting, GraphQL, and N+1

The current API uses REST query parameters for filtering and paging: `GET /api/orders?customerId=&status=&page=&pageSize=&sort=` and `GET /api/customers?search=&page=&pageSize=`. The order list accepts the documented `date` and `total` sort choices, caps `pageSize` at 100, projects to summary DTOs, and uses `AsNoTracking()`. The customer search similarly filters by name/email, sorts by name, and applies `Skip`/`Take`.

The current list response contains `items`, `page`, and `pageSize`, but does not calculate or return a total-row count. For clients that need a last page or total, I would add a separate `CountAsync()` using the same filters and return `totalCount` (and optionally `totalPages`). Offset pagination is simple and matches the current API; keyset pagination can be preferable for deep, frequently changing lists. Every sort choice should be whitelisted, not interpolated from arbitrary input.

The order detail query includes line items in one EF query, while the list endpoint selects summaries and does not load a collection per row. Those choices avoid the common N+1 pattern of issuing one line-item query for every listed order. GraphQL can be a better fit when clients need many different nested projections in one request, but it adds schema, authorization, query-cost, and caching considerations. This project has no GraphQL endpoint, so REST is the smaller and clearer fit for its current screens.

### 7. Security: JWT, claims, roles, and policies

The API currently validates bearer JWTs itself; it does not use Microsoft.Identity.Web or an Entra tenant. Startup requires an issuer, audience, signing key of at least 32 bytes, and a bounded access-token lifetime. JWT validation checks issuer, audience, signature, and lifetime with a short clock skew. The token-validated hook maps the token subject to an application user, and authorization policies require the `Orders.Read`, `Orders.Write`, or `Orders.Admin` permission/role. For example, order listing requires `Orders.Read`, creation requires `Orders.Write`, and manual status changes require `Orders.Admin`.

For Entra in production, I would configure Microsoft.Identity.Web against the tenant's issuer/audience, validate signing keys and lifetime, map Entra app roles or scopes into the application's permission model, and continue to authorize through named policies rather than trusting UI state. Access tokens should be short-lived and acquired through supported OAuth/OIDC flows; signing and refresh-token policy belong to the identity provider. The frontend's development role selector is not a security boundary: the API authorizes every protected request.

### 8. RabbitMQ: exchanges, durability, delivery, and idempotency

The project declares durable direct exchanges (`orders.events`, `orders.retries`, and `orders.dead-letter`) and durable queues, including `orders.order-created` and its dead-letter queue. A direct exchange is appropriate here because each routing key targets a named event queue. Order creation writes both the order and an `OutboxMessage` in the same database save; a hosted publisher later sends the event. This avoids the database-commit/message-publish gap that would exist if the request independently committed SQL and then published to RabbitMQ.

The publisher sends persistent messages with a message ID and publisher confirms enabled. The worker uses manual acknowledgements, a prefetch of one, and retry queues with delays of 5 seconds, 30 seconds, and 2 minutes; after three scheduled retries, a failed delivery is dead-lettered. This is at-least-once delivery: a message may be delivered again, so consumers must be idempotent. `OrderCreatedHandler` stores processed message IDs in `ProcessedMessages` in the same database save as its order update and handles duplicate/concurrent processing. The unique message ID key is the durable deduplication guard. Publisher confirms and a transactional outbox improve reliability but do not make SQL Server and RabbitMQ a single distributed transaction.

### 9. Frontend: state management and type safety

The SPA uses TanStack Query for server state, mutations, caching, and invalidation; React component/form state remains local where appropriate. This avoids putting fetched customer/order data in a global Redux store when the server is its source of truth. If the application later needs substantial shared client-only state or complex cross-feature workflows, Context or Redux Toolkit could be added for that specific state rather than replacing query caching.

API contracts are generated from the backend's Development Swagger document into `src/api/schema.d.ts` with `openapi-typescript`, and the frontend uses `openapi-fetch` for typed requests. The CI job starts the API and runs the API schema drift check before lint, TypeScript checking, frontend tests, and build. Zod and React Hook Form are used for client-side form validation, but server validation remains authoritative. Access tokens are held in memory and cleared on sign-out or a 401 rather than persisted in browser storage.

### 10. Testing and CI

The test strategy is a pyramid: fast domain/service unit tests for deterministic rules; API integration tests for HTTP, authentication/authorization, serialization, validation, and persistence interactions; and a small number of end-to-end browser tests for critical user journeys if/when those are added. This repository has xUnit domain-rule and validator tests, service tests for status idempotency and outbox behavior, plus API/authentication/role integration tests. The test application uses SQLite in-memory for service tests where SQL Server-specific behavior is not under test.

GitHub Actions restores, builds, and tests the .NET solution and separately installs, checks the generated API contract, lints, type-checks, tests with coverage, builds, and container-builds the SPA. I would add browser E2E coverage for create-customer/create-order/status-change, a SQL Server migration-application check, and dependency/container/security scanning as appropriate for deployment. Coverage thresholds can be useful when they protect critical behavior, but I would not use a raw percentage as a substitute for meaningful assertions. The current workflow does not claim to run E2E or a security scan.

### 11. Performance, scalability, and observability

The project has some scale-conscious foundations: order reads use no-tracking projections, pagination is capped, and SQL indexes support the main customer/status/date list plus the date-ordered list. Order creation is decoupled from downstream work with a SQL outbox and RabbitMQ worker, so request latency does not wait for fulfillment. Under a write spike, I would bound publisher/worker concurrency, monitor queue depth and oldest-message age, apply admission control or rate limits when necessary, and scale consumers based on backlog while preserving idempotency.

The React API client sends an `X-Correlation-ID`, and the API response envelope includes the request endpoint; useful next steps are to ensure the API accepts or creates a validated correlation ID, include it in structured logs, and propagate it in the outbox event to the worker. The project has messaging diagnostics but does not currently expose the requested `/healthz` and `/readiness` endpoints or a metrics/tracing/SLO implementation. I would add liveness/readiness checks for the API's SQL dependency and the worker's broker/database dependencies, plus request-duration/error-rate and queue metrics. Caching (for example an ETag on order detail) should be based on measured read patterns and invalidation semantics; it is not currently implemented.

## SQL questions

The examples use the actual table names and columns from `InitialCreate`: `Customers`, `Orders`, and `OrderLineItems`. `Orders.Status` is stored as `varchar(20)`, money is `decimal(18,2)`, and `Orders.RowVersion` is SQL Server `rowversion`. All timestamps in the API are written as UTC `DateTime`.

### 12. Customer orders: page plus total count

The API currently uses `Skip`/`Take` and the `(CustomerId, Status, CreatedAt)` index, but does not return a total count. For the requested status/date listing and a separate total, the SQL Server pattern is:

```sql
SELECT
    o.Status,
    o.CreatedAt,
    o.TotalAmount
FROM dbo.Orders AS o
WHERE o.CustomerId = @CustomerId
ORDER BY o.CreatedAt DESC
OFFSET @Offset ROWS
FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT_BIG(*) AS TotalCount
FROM dbo.Orders AS o
WHERE o.CustomerId = @CustomerId;
```

The parameters are typed values (`@CustomerId uniqueidentifier`, `@Offset int`, `@PageSize int`), not concatenated SQL. Validate that offset is non-negative and page size is within the API's 1–100 limit. If a status filter is added, apply the same `o.Status = @Status` predicate to both queries. With a status equality predicate, the existing composite index supports customer/status filtering and created-date ordering; SQL Server can scan the date key backwards for descending order. Without a status predicate, `Status` sits between `CustomerId` and `CreatedAt`, so the index may require a sort; if that is a frequent measured query, consider `(CustomerId, CreatedAt) INCLUDE (Status, TotalAmount)`. For consistency across the two statements during concurrent writes, run them under an appropriate snapshot transaction if the caller needs an exact page/count pair.

### 13. Top 10 spenders in the last 90 days, including zero-order customers

`Order.TotalAmount` is denominated in each order's `CurrencyCode`; summing ZAR, USD, and other currencies together is not a valid financial ranking. The schema has no exchange-rate table or conversion service. The query below therefore deliberately ranks within one selected currency (for example, `ZAR`); use the actual currency for the report. To rank all currencies together, first convert using an auditable, dated FX-rate source and state the reporting currency.

```sql
DECLARE @CurrencyCode char(3) = 'ZAR';
DECLARE @AsOfUtc datetime2(7) = SYSUTCDATETIME();

SELECT TOP (10)
    c.Id AS CustomerId,
    c.Name,
    c.Email,
    COALESCE(SUM(o.TotalAmount), CONVERT(decimal(38, 2), 0)) AS TotalSpend,
    @CurrencyCode AS CurrencyCode
FROM dbo.Customers AS c
LEFT JOIN dbo.Orders AS o
    ON o.CustomerId = c.Id
   AND o.CurrencyCode = @CurrencyCode
   AND o.CreatedAt >= DATEADD(day, -90, @AsOfUtc)
   AND o.CreatedAt < @AsOfUtc
GROUP BY c.Id, c.Name, c.Email
ORDER BY TotalSpend DESC, c.Id;
```

The date and currency predicates belong in the `LEFT JOIN` condition; putting them in `WHERE` would remove customers without matching orders. `COALESCE` makes those customers' totals zero. If the desired period is anchored to a calendar date rather than the current instant, bind an explicit UTC as-of value.

### 14. Indexing for customer/status/date reads

The model configures this index, including its SQL Server covering columns:

```sql
CREATE INDEX IX_Orders_CustomerId_Status_CreatedAt
ON dbo.Orders (CustomerId, Status, CreatedAt)
INCLUDE (CurrencyCode, TotalAmount);
```

It matches the common equality predicates (`CustomerId`, optionally `Status`) followed by the date order/range and covers the order-list projection (`Status`, `CreatedAt`, `CurrencyCode`, `TotalAmount`) without needing to fetch those values from the base row. The schema also defines `IX_Orders_CreatedAt` including customer, status, currency, and total for date-oriented listing, and `IX_OrderLineItems_OrderId` including SKU, quantity, and unit price for order detail.

Included columns are leaf-level payload, not additional sort/search keys. They reduce key lookups for these projections but increase index storage, write amplification, and maintenance cost; include only columns justified by measured queries. The primary key is `Id`; SQL Server's default EF migration creates it as clustered unless configured otherwise. For this workload, keep a narrow and stable clustered key unless actual plans and measurements justify changing it. The two secondary indexes are intentional only while their different query patterns justify their write/storage cost.

### 15. Execution plans and key lookups

For a representative query joining an order to its line items, inspect the **actual** execution plan in SSMS/Azure Data Studio and look for a `Key Lookup (Clustered)` or `RID Lookup` operator beneath a nested-loops join. The operator properties identify the target table/index, columns fetched, and estimated/actual executions; a high execution count often means one base-row fetch per matching index row. Compare estimated and actual row counts and use `SET STATISTICS IO, TIME ON` to confirm that the lookup is causing meaningful reads or latency.

The current indexes are designed to cover a common details query:

```sql
SELECT o.Id, o.Status, o.CreatedAt, o.TotalAmount,
       li.ProductSku, li.Quantity, li.UnitPrice
FROM dbo.Orders AS o
JOIN dbo.OrderLineItems AS li ON li.OrderId = o.Id
WHERE o.CustomerId = @CustomerId
  AND o.Status = @Status;
```

The order index includes `TotalAmount`; `Id` is already available through the order's clustered key, and the line-item index on `OrderId` includes the three selected item columns. If a different query still has a lookup, add only the frequently selected missing columns as `INCLUDE` columns to the narrowest suitable index, then check write cost and confirm the actual plan improves. Do not add columns blindly based only on a graphical operator.

### 16. `rowversion` and optimistic concurrency

`Order.RowVersion` is configured with `.IsRowVersion()` and exists as SQL Server's generated `rowversion` column. EF Core automatically uses the original version in an update's `WHERE` clause. A safe API update should retain the version observed by the client and translate a failed compare-and-swap into a conflict:

```csharp
var entry = dbContext.Entry(order);
entry.Property(o => o.RowVersion).OriginalValue = expectedRowVersion;
order.Status = targetStatus;
order.UpdatedAt = DateTime.UtcNow;

try
{
    await dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateConcurrencyException)
{
    var current = await dbContext.Orders
        .AsNoTracking()
        .Where(o => o.Id == order.Id)
        .Select(o => new { o.Id, o.Status, o.RowVersion })
        .SingleOrDefaultAsync(cancellationToken);

    return Conflict(new
    {
        message = "The order changed since it was read. Reload it before retrying.",
        current
    });
}
```

In a complete implementation, set the entry's `RowVersion` **original value** from the client's `If-Match`/expected version before saving; do not overwrite the database-generated current version. This repository exposes `RowVersion` in `OrderDto` and the worker handles concurrency exceptions, but the API transition request currently does not require the expected version or return a dedicated concurrency response. Its `Idempotency-Key` protects repeated identical transitions, a different concern.

### 17. Reader/writer deadlocks

A possible deadlock is a report transaction reading orders and line items in one access order while a status-update transaction holds an order lock and then modifies related data in the opposite order. Concurrent transactions can each hold a resource the other needs, producing a cycle; SQL Server chooses a victim and returns error 1205. Long-running reports and transactions that touch rows in inconsistent order increase the risk.

Start with short transactions, supporting indexes, and consistent access/update order; retry a deadlock victim only when the operation is safe and bounded. For a read-heavy SQL Server workload, `READ_COMMITTED_SNAPSHOT` can reduce reader/writer blocking by serving committed row versions, but it consumes version-store space and does not remove writer/writer deadlocks. Enable and monitor it as a database-level decision. Avoid `NOLOCK` or locking hints as a default because they can return inconsistent data or introduce new contention.

### 18. Running order spend by customer

The required window-function shape is:

```sql
SELECT
    o.CustomerId,
    o.CurrencyCode,
    o.CreatedAt,
    o.Id AS OrderId,
    o.TotalAmount,
    SUM(o.TotalAmount) OVER
    (
        PARTITION BY o.CustomerId, o.CurrencyCode
        ORDER BY o.CreatedAt, o.Id
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningSpend
FROM dbo.Orders AS o
ORDER BY o.CustomerId, o.CurrencyCode, o.CreatedAt, o.Id;
```

The assessment's essential expression is `SUM(TotalAmount) OVER (PARTITION BY CustomerId ORDER BY CreatedAt)`. This schema has multiple permitted currencies, so the example also partitions by `CurrencyCode` to avoid adding unlike monetary units. `Id` makes the ordering deterministic when multiple orders have the same timestamp. If the report first converts all amounts to a common currency, use that converted amount and partition only by customer.

### 19. Partitioning strategy for millions of orders

If measured volume and retention justify it, I would evaluate monthly `RANGE RIGHT` partitions on `CreatedAt`, with boundaries aligned to UTC month starts. This supports partition elimination for date-range reports and can make archival/deletion operations more manageable. I would retain aligned indexes, monitor partition sizes and skew, and test actual query plans before adopting partitioning; it does not automatically make every query faster.

For archival, create a staging/archive table with exactly matching columns, indexes, and compatible constraints, then switch an old, isolated partition to the staging table and export or retain it according to policy. Partition switching has strict schema/index/check-constraint requirements and is a metadata operation only when those requirements are met. This schema's primary key is `Id`; a partitioned unique clustered index generally must include the partitioning column. Moving from the current GUID primary-key layout to a partition-aligned design therefore needs deliberate key/index design and migration planning. I would not partition the current dataset prematurely.

### 20. `sp_GetTransactionReport`

This procedure uses the requested filter parameters and produces two result sets: order/line detail, then an order-level summary. The end date is inclusive by using a half-open datetime range (`< day after @EndDate`), which also includes all times on the final date. The filter is materialized once so both result sets refer to the same set of matching orders:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_GetTransactionReport
    @StartDate date,
    @EndDate date,
    @Status nvarchar(20) = NULL,
    @CustomerId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @EndDate < @StartDate
        THROW 50001, 'EndDate must be on or after StartDate.', 1;

    SELECT
        o.Id,
        o.CustomerId,
        o.Status,
        o.CreatedAt,
        o.CurrencyCode,
        o.TotalAmount
    INTO #FilteredOrders
    FROM dbo.Orders AS o
    WHERE o.CreatedAt >= CONVERT(datetime2(7), @StartDate)
      AND o.CreatedAt < DATEADD(day, 1, CONVERT(datetime2(7), @EndDate))
      AND (@Status IS NULL OR o.Status = @Status)
      AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId);

    -- Result set 1: one row per order line; orders with no lines are retained.
    SELECT
        c.Name,
        c.Email,
        c.CountryCode,
        f.Id AS OrderId,
        f.Status,
        f.CreatedAt,
        f.CurrencyCode,
        f.TotalAmount,
        li.ProductSku,
        li.Quantity,
        li.UnitPrice
    FROM #FilteredOrders AS f
    JOIN dbo.Customers AS c ON c.Id = f.CustomerId
    LEFT JOIN dbo.OrderLineItems AS li ON li.OrderId = f.Id
    ORDER BY f.CreatedAt, f.Id, li.Id;

    -- Result set 2: one row per report, including an empty-range result.
    SELECT
        COUNT_BIG(*) AS TotalOrders,
        COALESCE(SUM(f.TotalAmount), CONVERT(decimal(38, 2), 0))
            AS GrandTotalAmount
    FROM #FilteredOrders AS f;
END;
```

The model stores each `TotalAmount` in its order's `CurrencyCode`; consequently, `GrandTotalAmount` above is only a nominal arithmetic sum if the selected orders span currencies. A financially meaningful cross-currency report must either filter to a single currency or convert each order using an explicit, auditable FX-rate source and report currency. The existing `IX_Orders_CustomerId_Status_CreatedAt` supports customer/status filtering, and `IX_Orders_CreatedAt` supports date scans; if this report is frequent, inspect its actual plan and consider a measured covering-index adjustment. Because the schema stores `Status` as `varchar(20)` while the requested procedure parameter is `nvarchar(20)`, use a matching SQL type or verify the actual plan/conversion behavior for the target collation and workload.
