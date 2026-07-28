# ADR 0002: Checkout Command Idempotency Contract

## Status

Proposed. Oracle workshop output; approval is required from Product, Orders Engineering, Security and QA before implementation.

## Context

`POST /api/v1/orders` currently identifies the caller from the authenticated customer subject, loads the current basket, creates a new order and `OrderCreatedV1` outbox message in one database save, and then attempts to clear the basket.

The command has no stable client-supplied identity. A browser retry after a timeout, a double submit that reaches the server, or two concurrent identical requests can therefore create more than one order and more than one downstream checkout workflow. Disabling the frontend button reduces accidental duplicate clicks but is not a correctness control.

The current request contains only `customerEmail` and `paymentMethod`; the authoritative items and totals come from the Basket Service. The creator must persist that snapshot in the order, but a completed replay cannot safely reload it: the first successful execution normally clears the basket and the customer may later start a new basket.

## Oracle workshop decisions

The following decisions form the recommended oracle. Approval of this ADR approves them as one contract.

| Decision | Recommended contract | Reason |
|---|---|---|
| `ORCL-ORD-IDEMP-001` Command identity | `Idempotency-Key` is required for `POST /api/v1/orders`. The frontend generates one UUID v4 when a checkout submission starts and reuses it for retries of that submission. A deliberate new submission generates a new key. | The identity survives HTTP retries and is independent of a server-generated order ID. |
| `ORCL-ORD-IDEMP-002` Validation | Treat the key as an opaque, case-sensitive ASCII value of 1–128 characters. Reject a missing or malformed key with `400 application/problem+json`. Do not log the raw value; log a one-way digest or the resulting order ID. | Bounded opaque keys are interoperable and avoid putting customer data into logs or indexes. |
| `ORCL-ORD-IDEMP-003` Scope | Uniqueness is scoped by authenticated `customerId`, operation name `CreateOrder`, and key. A key used by another customer is unrelated. | Prevents cross-customer discovery and permits keys to be generated independently. |
| `ORCL-ORD-IDEMP-004` Request fingerprint | The fingerprint covers normalized `customerEmail`, normalized `paymentMethod` and any future client-supplied command fields. The creator loads the basket exactly once and persists its snapshot in the order; the basket is not reloaded to validate a completed replay. | A replay must still work after the first execution clears the basket. A changed basket represents a new submit intent and therefore requires a new key. |
| `ORCL-ORD-IDEMP-005` Same key and fingerprint | Create exactly one order, one initial status-history entry and one `OrderCreatedV1` outbox message. A completed replay returns that same order and `Location`, with status `200 OK` and `Idempotent-Replayed: true`; the first successful response remains `201 Created`. No replay clears the basket or emits another event. | The resource identity and downstream side effects are stable while the response makes replay observable. |
| `ORCL-ORD-IDEMP-006` Same key, different fingerprint | Return `409 Conflict` with ProblemDetails type `urn:eshop:problem:idempotency-key-reused`, without creating, updating, publishing or clearing anything. | Reusing a command identity for different client-supplied intent is a conflict, not a retry. |
| `ORCL-ORD-IDEMP-007` Concurrent duplicate | Exactly one request becomes the creator. A concurrent identical request waits for the bounded database transaction outcome and then returns the replay response. It must not call Basket Service again after an existing completed record is resolved. | A unique constraint alone prevents duplicates but does not define a useful response for the losing request. |
| `ORCL-ORD-IDEMP-008` Atomic persistence | Store the idempotency record, fingerprint, order reference, response state and order/outbox changes in the same PostgreSQL transaction. Enforce a unique index on `(customer_id, operation, idempotency_key)`. | Process-local locks do not protect multiple replicas or crashes. |
| `ORCL-ORD-IDEMP-009` Failure semantics | Validation and business rejections before commit are not cached. A committed order is replayable even when basket clearing fails or the original HTTP response is lost. Unexpected failures roll back both the order and idempotency record and may be retried with the same key. | Only durable business outcomes should become the retry oracle. |
| `ORCL-ORD-IDEMP-010` Retention | Retain the idempotency identity for at least the lifetime of its order. Do not expire it independently while the order is queryable. | Short TTL expiry would allow a late network retry to create a second order. |

## API examples

First submission:

```http
POST /api/v1/orders HTTP/1.1
Authorization: Bearer <token>
Idempotency-Key: 7b814347-73c4-4f67-a6f8-e46f6cabf4d5
Content-Type: application/json

{"customerEmail":"alice@example.com","paymentMethod":"test-success"}
```

Expected result: `201 Created`, one stable `Location`, one order and one outbox message.

Identical replay: `200 OK`, the same body and `Location`, plus `Idempotent-Replayed: true`.

Same key after the email or payment method changes: `409 Conflict`, with no new durable side effect. A basket change alone does not mutate a completed replay; a new checkout intent must use a new key.

## Shift-left acceptance criteria for TECH-02

1. The OpenAPI contract documents the required header and the `400`, `409`, `200` replay and `201` first-response semantics.
2. PostgreSQL migration adds the idempotency persistence and composite unique constraint; a rollback migration is reviewed.
3. The application performs order, status history, outbox and idempotency persistence atomically without process-local correctness locks.
4. The frontend creates one UUID per submit intent, retains it across retryable transport failures, and replaces it only when the user starts a new intent.
5. API tests prove missing/invalid key, first success, sequential replay without a basket reload, concurrent replay, changed HTTP request conflict, changed-basket replay of the original order, new-key use of the current basket, lost-response replay, basket-clear failure replay and customer-scope isolation.
6. Messaging evidence proves replay creates only one `OrderCreatedV1` and one downstream payment/inventory workflow.
7. Logs and ProblemDetails contain correlation data but never the raw key, bearer token or customer basket.
8. TestRail binds the new direct tests to `ESHOP-ORDER-002`; the release evidence includes first-attempt and repeat/flake results.

## Implementation notes

A dedicated idempotency table is preferred over storing only the key on `orders`: it can represent an in-progress claim, the request fingerprint and response metadata while keeping the order aggregate focused. The implementation must use PostgreSQL uniqueness as the cross-replica arbiter. A bounded wait must end in an explicit retryable response if the creator transaction cannot be resolved; it must never fall through to a second creation attempt.

Canonicalization must be implemented once in Orders Service and tested with casing and normalization variants. Hashing is an implementation detail; equality is defined by the normalized client-supplied business fields above, not by raw JSON byte equality.

## Consequences

- Checkout retries become safe across browser, gateway and service replicas.
- A changed basket under the same command key is visible as a conflict rather than silently creating an unexpected order.
- Order creation gains additional persistence and concurrency paths that require migration, cleanup and observability work.
- Clients that do not send `Idempotency-Key` become incompatible when the contract is activated; rollout must update the frontend and any API consumers together.

## Out of scope

- Idempotency of payment, inventory and notification consumers; those keep their event/inbox identities.
- Basket mutation concurrency and the product-price freshness policy.
- Automatic retry of semantic `400` or `409` responses.

## Approval record

| Role | Decision | Name / reference | Date |
|---|---|---|---|
| Product | Pending | — | — |
| Orders Engineering | Pending | — | — |
| Security | Pending | — | — |
| QA | Pending | — | — |
