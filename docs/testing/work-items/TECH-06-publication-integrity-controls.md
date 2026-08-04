# TECH-06 — Implement publication-integrity controls

| Field | Value |
|---|---|
| Type | Engineering/CI |
| Status | Accepted |
| Owner | CI Engineering |
| Priority | High |
| Related | QA-04 |

## Outcome

Implement the fail-closed evidence boundary defined by QA-04 and make the E2E runner checks portable across the supported CI environment.

## Implementation scope

- Require all upstream test areas to succeed before publication.
- Validate presence, non-empty content, XML structure and exact report cardinality.
- Complete validation and Automation ID preflight before remote calls.
- Cover publication rejection paths with deterministic unit tests.
- Isolate the E2E port probe behind a Linux/Windows contract and run Bash syntax validation and ShellCheck.

## Acceptance criteria

1. A failed or skipped required upstream job prevents publication.
2. Missing, empty, malformed or cardinality-incomplete report sets fail before TestRail mutation.
3. A complete valid Main set publishes exactly four closed runs.
4. Quality-policy tests cover both valid and invalid artifact sets.
5. The E2E port check uses portable `ss` options and has deterministic platform contract tests.
6. Selector, binding and TestIntent catalogue remain unchanged.

## Oracle

For every CI event eligible to publish, the local preflight result predicts remote behavior: invalid evidence set means zero remote runs; valid exact evidence set means one complete closed publication set.

## Result

Accepted on commit `b259026` through Main CI #56 and TestRail R82–R85. Quality policy passed 38/38, TestRail tooling 7/7 and the Bash port contract passed.

## Source records

- [`QA-04-fail-closed-testrail-publication.md`](QA-04-fail-closed-testrail-publication.md)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-04--tech-06-evidence)
- [`../testrail-ci-integration.md`](../testrail-ci-integration.md)

