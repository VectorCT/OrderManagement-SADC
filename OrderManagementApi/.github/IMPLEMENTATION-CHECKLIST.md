# Backend Implementation Checklist

This checklist tracks the recommended implementation sequence in the [backend implementation plan](../docs/backend-implementation-plan.md). Update task statuses as work progresses.

**Status key:** `[x]` Complete · `[ ]` Not complete

## 1. Service layer implementation — Complete

- [x] Add Customer and Order service interfaces and implementations.
- [x] Add the SADC currency validation policy and order state machine.
- [x] Keep business rules in services, not entity models.

## 2. Validation and DTO contracts — Complete

- [x] Add FluentValidation for request shape and static business rules.
- [x] Separate DTOs from persistence models.
- [x] Validate customer existence, currency-country pairing, and status transitions in the service layer.

## 3. API endpoints and auth — In progress

- [ ] Implement thin controllers for customers and orders.
- [ ] Add JWT/role policy configuration for `Orders.Read`, `Orders.Write`, and `Orders.Admin`.
- [ ] Add secure defaults and problem details.
- [ ] Apply consistent pagination standards to the API.

## 4. Messaging and worker flow — Not started

- [ ] Publish `OrderCreated` events.
- [ ] Build the worker to consume and fulfill orders.
- [ ] Include idempotency replay/conflict handling and retry/DLQ handling.

## 5. Tests — In progress

- [x] Add unit tests for request validation.
- [ ] Add unit tests for SADC rules and transition logic.
- [ ] Add integration tests for endpoints and database behavior.
- [ ] Add idempotency tests for status update replay and conflict handling.

## 6. DevOps and project docs — In progress

- [ ] Add a CI pipeline and Docker Compose setup.
- [ ] Add Postman or `.http` examples for the customer and order API.
- [ ] Prepare the README and `ANSWERS.md` for the final assessment submission.
