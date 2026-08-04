# QA-02 — Prove duplicate checkout across the complete workflow

| Field | Value |
|---|---|
| Type | QA integration |
| Status | Accepted with residual |
| Owner | QA / Checkout workflow |
| Priority | Critical |
| Related | TECH-02, GAP-002 |

## Outcome

Prove that sequential or concurrent delivery of the same checkout intent creates exactly one order and one complete downstream workflow.

## Preconditions

- TECH-02 idempotency contract is implemented.
- Real Orders, Inventory, Payments and Notifications hosts run with PostgreSQL and RabbitMQ Testcontainers.
- The same authenticated customer, basket, request fingerprint and `Idempotency-Key` are used for both calls.

## Acceptance criteria

1. A sequential duplicate produces one `201 Created` response and one `200 OK` replay.
2. A synchronized concurrent duplicate produces exactly one creator and one replay.
3. Both responses identify the same order and use the same absolute `Location`; replay includes `Idempotent-Replayed: true`.
4. Persistence contains exactly one order and one idempotency record.
5. Inventory is reserved once and payment is authorized once.
6. Exactly four expected notifications and the defined Orders/Inventory/Payments outbox and inbox cardinalities exist.
7. Workflow queues and dead-letter queues are empty after bounded stabilization.
8. The variants bind to both `ESHOP-ORDER-002` and `ESHOP-E2E-001`.

## Oracle

Duplicate transport delivery may change the HTTP response from creator to replay, but it must not multiply durable business state, messages or externally visible workflow effects.

## Result and residual

Accepted on commit `a1fba95` through CI #33 and TestRail R38. The full Messaging suite passed 12/12, Orders 17/17 and the concurrent scenario five independent local runs. Scheduled longitudinal repeat history remains a maturity residual, not an implementation defect.

## Source records

- [`../../architecture/0002-checkout-command-idempotency.md`](../../architecture/0002-checkout-command-idempotency.md)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-02-cross-service-evidence)
- [`../traceability-matrix.md`](../traceability-matrix.md)

