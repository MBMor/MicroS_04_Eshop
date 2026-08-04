# QA and Engineering Work Items

This directory is backlog of the QA and engineering work items.

The work-item files preserve the groomed problem, outcome, scope, oracle and acceptance criteria. Execution evidence remains authoritative in [`../evidence-baseline.md`](../evidence-baseline.md), coverage and residual gaps in [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md), and cross-artifact mappings in [`../traceability-matrix.md`](../traceability-matrix.md). An accepted work item does not activate a future quality gate.

## Status vocabulary

- **Accepted** — the defined acceptance criteria have shared CI/TestRail evidence.
- **Accepted with residual** — the defined change is accepted, but explicitly named longitudinal or adjacent evidence remains open.
- **Superseded as a record** — the original deliverable was completed and a newer baseline now carries the continuing record.

## Catalogue

| ID | Title | Type | Status | Related gap/capability |
|---|---|---|---|---|
| [QA-01](QA-01-executable-evidence-baseline.md) | Establish executable evidence baseline | QA governance | Superseded as a record | Portfolio baseline |
| [QA-02](QA-02-duplicate-checkout-workflow-proof.md) | Prove duplicate checkout across the complete workflow | QA integration | Accepted with residual | GAP-002 / `ESHOP-ORDER-002`, `ESHOP-E2E-001` |
| [QA-03](QA-03-governed-test-tiers.md) | Introduce governed PR, Main, Nightly and Release tiers | QA/CI governance | Accepted | GAP-022 |
| [QA-04](QA-04-fail-closed-testrail-publication.md) | Make TestRail publication fail closed | QA/CI governance | Accepted | Evidence integrity |
| [QA-05](QA-05-readiness-oracle-workshop.md) | Approve dependency-aware readiness oracle | QA architecture | Accepted | GAP-004 / `ESHOP-RESILIENCE-002` |
| [QA-TR-01](QA-TR-01-testrail-case-readability.md) | Make TestRail cases independently executable | QA/TestRail governance | Accepted | TestRail C49–C93 |
| [TECH-01](TECH-01-inventory-concurrency.md) | Enforce inventory concurrency invariants | Engineering/testing | Accepted with residual | GAP-001 / `ESHOP-INVENTORY-002` |
| [TECH-02](TECH-02-checkout-command-idempotency.md) | Implement checkout command idempotency | Engineering | Accepted with residual | GAP-002 / `ESHOP-ORDER-002` |
| [TECH-03](TECH-03-atomic-negative-mutations.md) | Enforce traceable atomic rejection | Engineering/testing | Accepted | GAP-020 / `ESHOP-DATA-004` |
| [TECH-04](TECH-04-problemdetails-media-type.md) | Enforce ProblemDetails media type | Engineering | Accepted | GAP-020 / `ESHOP-DATA-004` |
| [TECH-05](TECH-05-gateway-authorization-matrix.md) | Enforce gateway authorization matrix | Engineering/testing | Accepted | GAP-026 / `ESHOP-GW-001` |
| [TECH-06](TECH-06-publication-integrity-controls.md) | Implement publication-integrity controls | Engineering/CI | Accepted | Evidence integrity |
| [TECH-07](TECH-07-catalog-mutation-boundary.md) | Enforce Catalog mutation boundary | Engineering/security | Accepted | GAP-003 / `ESHOP-CATALOG-001` |
| [TECH-08](TECH-08-dependency-aware-readiness.md) | Implement dependency-aware readiness | Engineering/platform | Accepted | GAP-004 / `ESHOP-RESILIENCE-002` |

## Maintenance rule

Create or groom a work item here before implementation. Keep outcome and acceptance criteria stable during delivery; record design changes explicitly. After shared validation, update status and link immutable evidence instead of copying full CI logs into the ticket.
