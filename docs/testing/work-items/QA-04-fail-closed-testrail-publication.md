# QA-04 — Make TestRail publication fail closed

| Field | Value |
|---|---|
| Type | QA/CI governance |
| Status | Accepted |
| Owner | QA / CI |
| Priority | High |
| Related | TECH-06 |

## Outcome

Prevent incomplete or misleading TestRail evidence when an upstream test area fails or its report is missing, empty, malformed or cardinality-incomplete.

## Problem

A diagnostic Main execution demonstrated that partial artifacts could be published even though Checkout E2E had failed. Individually valid results were not a valid complete Main evidence set.

## Acceptance criteria

1. TestRail publication requires successful Change scope, Backend, Frontend and Checkout E2E jobs.
2. All four expected JUnit reports must exist, be non-empty and parse as XML.
3. Aggregate counts must exactly match the governed Main contract before Automation ID preflight.
4. Validation happens before the first remote TestRail mutation.
5. Missing, malformed, empty, duplicate or incomplete reports fail with actionable diagnostics.
6. Failure paths are covered deterministically without deliberately failing Main to produce external evidence.
7. The positive path creates one complete set of four closed TestRail runs.

## Oracle

Publication is atomic at the evidence-set boundary: either every required upstream result and exact aggregate is valid, or no new TestRail run is created.

## Result

Accepted with TECH-06 through Main CI #56 and TestRail R82–R85. The four runs passed at `12/22/3/4`; negative paths are covered by policy tests.

## Source records

- [`../evidence-baseline.md`](../evidence-baseline.md#qa-04--tech-06-evidence)
- [`../testrail-ci-integration.md`](../testrail-ci-integration.md)
- [`TECH-06-publication-integrity-controls.md`](TECH-06-publication-integrity-controls.md)

