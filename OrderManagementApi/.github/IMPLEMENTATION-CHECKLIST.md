# Backend Implementation Checklist

This checklist tracks the recommended implementation sequence in the [backend implementation plan](../docs/backend-implementation-plan.md). Update task statuses as work progresses.

**Status key:** `[x]` Complete · `[ ]` Not complete

## 1. Service layer implementation — Complete

- [x] Add Customer and Order service interfaces and implementations.
- [x] Add the SADC currency validation policy and order state machine.
- [x] Keep business rules in services, not entity models.

## 2. Validation and DTO contracts — In progress

- [x] Define FluentValidation rules for request shape and static constraints.
- [x] Invoke request validators at the customer and order API endpoints.
- [x] Separate DTOs from persistence models.
- [x] Validate customer existence, currency-country pairing, and status transitions in the service layer.

## 3. API endpoints and auth — In progress

- [x] Implement thin controllers for customers and orders.
- [x] Add JWT/role policy configuration for `Orders.Read`, `Orders.Write`, and `Orders.Admin`.
- [x] Add secure error defaults and consistent error response envelopes.
- [x] Apply consistent one-based pagination with bounded page sizes to the API.

## 4. Messaging and worker flow — Not started

- [ ] Publish `OrderCreated` events.
- [ ] Build the worker to consume and fulfill orders.
- [ ] Include idempotency replay/conflict handling and retry/DLQ handling.

## 5. Tests — In progress

- [x] Add unit tests for request validation.
- [ ] Add unit tests for SADC rules and transition logic.
- [x] Add integration tests for endpoint routes, response envelopes, validation boundaries, authorization, and status codes.\r\n- [ ] Add integration tests for database behavior.
- [ ] Add idempotency tests for status update replay and conflict handling.

## 6. DevOps and project docs — In progress

- [ ] Add a CI pipeline and Docker Compose setup.
- [x] Add `.http` examples for the customer and order API.
- [ ] Prepare the README and `ANSWERS.md` for the final assessment submission.
