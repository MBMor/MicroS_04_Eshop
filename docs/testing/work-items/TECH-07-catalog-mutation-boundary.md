# TECH-07 — Enforce Catalog mutation boundary

| Field | Value |
|---|---|
| Type | Engineering/security |
| Status | Accepted |
| Owner | Catalog Engineering / Security |
| Priority | Critical |
| Related | GAP-003, `ESHOP-CATALOG-001` |

## Outcome

Keep public Catalog reads anonymous while preventing anonymous, customer and support callers from mutating Catalog even when they address Catalog Service directly.

## Scope

Add shared JWT/auth middleware and `AdminOnly` protection to Catalog POST/PUT/DELETE, direct no-write evidence, and gateway non-addressability/non-forwarding evidence. Full container-network isolation remains GAP-013.

## Acceptance criteria

1. Catalog configures shared JWT authentication and authorization in the correct middleware order.
2. POST, PUT and DELETE require `AdminOnly`; public GET remains anonymous.
3. Nine anonymous/customer/support mutation variants return 401/403 as appropriate and preserve product count and seeded values.
4. Existing authenticated admin CRUD remains functional; full Catalog passes 19/19.
5. Representative gateway POST/PUT/DELETE calls return 405 and forward zero requests; full gateway passes 68/68.
6. New selectors aggregate to `ESHOP-CATALOG-001`; the gateway selector also strengthens `ESHOP-GW-001`.
7. TestRail C53 retains stable identity and synchronized automated/implemented/approved metadata.
8. PR, Main and explicit Release pass before acceptance; `GATE-SEC-001` remains Future.

## Oracle

Authorization is proven at both trust boundaries: direct service denials must preserve PostgreSQL state, and the absent gateway surface must produce 405 with zero downstream forwarding.

## Result

Accepted after manifest hotfix `4ec560f`. Main CI #62/R87 and governed Release R90 passed, including `ESHOP-CATALOG-001` and `ESHOP-GW-001`; GAP-003 is closed.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#gap-003--tech-07-groomed-catalog-mutation-boundary-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#tech-07--gap-003-accepted-evidence)
- [`../traceability-matrix.md`](../traceability-matrix.md)

