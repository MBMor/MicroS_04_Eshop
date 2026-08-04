# TECH-03 — Enforce traceable atomic rejection

| Field | Value |
|---|---|
| Type | Engineering/testing |
| Status | Accepted |
| Owner | Catalog and Orders Engineering |
| Priority | High |
| Related | GAP-020, `ESHOP-DATA-004` |

## Outcome

Return actionable, correlatable errors for invalid Catalog and checkout mutations without partial persistence or accidental basket consumption.

## Scope

Strengthen the four existing `ESHOP-DATA-004` selectors. Do not add validation rules, selectors, TestRail cases or change tier ownership. ProblemDetails media-type standardization is handled separately by TECH-04.

## Acceptance criteria

1. Covered 400 responses include status, type, title, detail, instance, nonblank `traceId` and `requestId`.
2. Catalog invalid create names `Name`, reports `model_validation_failed` and leaves product count unchanged.
3. Empty-basket checkout returns deterministic `Checkout failed.` detail, does not clear the basket and leaves every Orders persistence table empty.
4. Mixed-currency checkout preserves both basket lines and leaves Orders persistence empty.
5. Invalid-email checkout names `CustomerEmail`, never calls Basket service, retains the basket and leaves Orders persistence empty.
6. Existing selector names, four bindings and PR/Main ownership remain unchanged.
7. Targeted Catalog 1/1 and Orders 3/3 pass before shared Main/TestRail acceptance.

## Oracle

Every rejected request must be diagnosable by its response and correlation identifiers, and observation of all relevant stores and collaborators must prove zero partial business effect.

## Result

Accepted through PR CI #41, Main CI #42 and TestRail R64 on commit `c587eb9`. GAP-020 was closed without changing risk, control or gate state.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#gap-020--tech-03-groomed-atomic-rejection-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#tech-03--gap-020-evidence)
- [`TECH-04-problemdetails-media-type.md`](TECH-04-problemdetails-media-type.md)

