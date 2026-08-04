# TECH-02 — Implement checkout command idempotency

| Field | Value |
|---|---|
| Type | Engineering |
| Status | Accepted with residual |
| Owner | Orders Engineering |
| Priority | Critical |
| Related | QA-02, GAP-002, `ESHOP-ORDER-002` |

## Outcome

Make browser retries, lost responses and concurrent duplicate submissions resolve to one logical order and one downstream checkout workflow.

## Contract

- `Idempotency-Key` is required, opaque, case-sensitive and limited to 1–128 visible ASCII characters.
- Identity is scoped by authenticated customer, operation and key.
- The request fingerprint covers normalized client-supplied command fields.
- The first success returns `201`; an identical completed replay returns `200`, the same body and `Location`, plus `Idempotent-Replayed: true`.
- Reusing the key for a different fingerprint returns atomic `409 Conflict`.
- Idempotency record, order, initial history and outbox event commit in one PostgreSQL transaction.

## Acceptance criteria

1. OpenAPI describes the required header and 400/409/200/201 behavior.
2. A migration creates durable idempotency state and a unique `(customer_id, operation, idempotency_key)` constraint with reviewed rollback.
3. Correctness relies on database uniqueness, not a process-local lock.
4. The frontend creates one UUID v4 per submit intent and retains it across retryable transport failures.
5. Tests cover missing/malformed key, first success, sequential and concurrent replay, changed-request conflict, changed basket, new key, lost response, basket-clear failure and customer isolation.
6. Replay does not reload or clear the basket and emits no second `OrderCreatedV1`.
7. Logs and ProblemDetails expose correlation but never raw key, bearer token or basket data.
8. Direct variants bind to `ESHOP-ORDER-002`; QA-02 proves complete downstream cardinality.

## Oracle

One customer operation and key identify one durable command outcome. A completed identical replay returns that outcome; a changed intent conflicts without side effects; an uncommitted failure may safely retry.

## Result and residual

Accepted on `a1fba95` through CI #33 and TestRail R38. Direct Orders/frontend variants and QA-02 workflow proof passed. Scheduled repeat history remains a longitudinal evidence residual.

## Source records

- [`../../architecture/0002-checkout-command-idempotency.md`](../../architecture/0002-checkout-command-idempotency.md)
- [`../evidence-baseline.md`](../evidence-baseline.md#tech-02-evidence)
- [`QA-02-duplicate-checkout-workflow-proof.md`](QA-02-duplicate-checkout-workflow-proof.md)

