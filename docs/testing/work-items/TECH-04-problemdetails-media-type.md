# TECH-04 — Enforce ProblemDetails media type

| Field | Value |
|---|---|
| Type | Engineering |
| Status | Accepted |
| Owner | Catalog Engineering |
| Priority | Medium |
| Related | GAP-020, `ESHOP-DATA-004` |

## Outcome

Make Catalog validation failures machine-readable as standard ProblemDetails JSON instead of generic JSON.

## Scope

Change shared invalid-model-state serialization and strengthen the existing invalid Catalog create selector. Do not add validation rules, redesign exception responses, add selectors/TestRail cases or normalize every consumer in this ticket.

## Acceptance criteria

1. `CreateProductInvalidRequestReturnsBadRequest` requires exact media type `application/problem+json`.
2. Status, canonical body fields, `Name` validation error, correlation identifiers and unchanged product count remain asserted.
3. The shared factory uses an explicit JSON result so content negotiation cannot downgrade the declared media type.
4. Targeted Catalog 1/1, full Catalog 10/10, Release solution build and governance tooling pass.
5. Existing selector, TestRail identity, tier ownership and locked report cardinality remain unchanged.

## Oracle

The response must simultaneously satisfy the transport contract (`400 application/problem+json`), semantic ProblemDetails body and zero-write invariant.

## Result

Accepted on commit `b298107` through PR CI #45, Main CI #46 and TestRail R72. No new TestRail case or gate lifecycle change was introduced.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#tech-04-groomed-problemdetails-media-type-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#tech-04-evidence)
- [`TECH-03-atomic-negative-mutations.md`](TECH-03-atomic-negative-mutations.md)

