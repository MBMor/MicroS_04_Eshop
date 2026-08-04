# QA-03 — Introduce governed PR, Main, Nightly and Release tiers

| Field | Value |
|---|---|
| Type | QA/CI governance |
| Status | Accepted |
| Owner | QA / CI |
| Priority | High |
| Related | GAP-022 |

## Outcome

Provide fast deterministic pull-request feedback while preserving cumulative Main coverage and explicit deeper Nightly and Release execution.

## Scope

- One fail-closed primary classification for every governed selector.
- Release as an explicit, reviewable overlap rather than a competing primary tier.
- Generated positive filters for mixed .NET projects.
- Exact selector, executable-row and TestRail publication cardinality.

## Acceptance criteria

1. Pull requests run quality policy, compile backend test projects, execute 66 backend-unit and 13 frontend rows, and use no Docker, browser or TestRail secrets.
2. Main push and manual dispatch execute cumulative PR + Main coverage: 66 backend-unit, 96 backend-integration, 13 frontend and 3 E2E rows at the accepted cutover baseline.
3. Nightly selects 19 logical selectors and publishes 11 TestIntent aggregates.
4. Release selects the approved overlap and publishes six aggregates at the cutover baseline.
5. Missing, duplicate, unknown or unclassified selectors and changed report cardinality fail closed.
6. Main publishes four closed TestRail runs at `12/22/3/4`; `trcli -n` creates no cases.
7. Direct pushes to Main retain PR coverage; rollback requires only reverting the cutover change.

## Oracle

Changing the GitHub event may alter depth and publication behavior, but never silently remove a governed selector from all tiers or permit a smaller-than-declared report set.

## Result

Accepted through Nightly R49, Release R50, PR CI #37 and cumulative Main CI #38/TestRail R55–R58. Later work items intentionally evolved counts while preserving this policy model.

## Source records

- [`../automated-test-gap-analysis.md`](../automated-test-gap-analysis.md#gap-022-groomed-prmain-cutover-contract)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-03-tier-policy-evidence)
- [`../../../scripts/quality/test-tier-policy.json`](../../../scripts/quality/test-tier-policy.json)

