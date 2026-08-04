# TECH-05 — Enforce gateway authorization matrix

| Field | Value |
|---|---|
| Type | Engineering/testing/security |
| Status | Accepted |
| Owner | Gateway Engineering / Security |
| Priority | Critical |
| Related | GAP-026, `ESHOP-GW-001` |

## Outcome

Give every endpoint addressable through the API Gateway an explicit reviewable access policy and prove that denied requests never reach downstream services.

## Scope

Reconcile the production gateway configuration with an authoritative registry covering proxy and local endpoints, authorization/rate-limit metadata, allowed roles and representative requests. Downstream ownership and production ingress/TLS remain out of scope.

## Acceptance criteria

1. The registry contains all 13 proxy routes and the known local endpoints with unique representative requests.
2. Policy validation fails closed on missing/extra routes or drift in cluster, path, method, authorization, rate limiter or roles.
3. The matrix proves anonymous access to public endpoints, anonymous denial for authenticated endpoints and wrong-role denial for protected endpoints.
4. Every configured allowed role has a positive variant.
5. Denied proxy calls produce zero downstream requests; successful proxy calls forward exactly once with expected method and path.
6. The full gateway suite and policy/TestRail tooling pass locally.
7. The Main-owned selector binds to existing `ESHOP-GW-001`; no TestRail case is created.
8. `GATE-SEC-001` remains Future and unevaluated.

## Oracle

The executable registry and runtime gateway configuration must describe the same addressable surface. Status, role decision and downstream request count together form the authorization oracle.

## Result

Accepted after the runner-portability hotfix `daf835d`; Main CI #52 and TestRail R78–R81 passed, including `ESHOP-GW-001` in R79. Later TECH-08 expanded the local endpoint inventory without changing this contract model.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#gap-026--tech-05-groomed-gateway-authorization-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#tech-05-evidence)
- [`../../../scripts/quality/gateway-route-policy.json`](../../../scripts/quality/gateway-route-policy.json)

