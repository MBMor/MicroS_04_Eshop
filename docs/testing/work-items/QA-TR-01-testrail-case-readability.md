# QA-TR-01 — Make TestRail cases independently executable

| Field | Value |
|---|---|
| Type | QA/TestRail governance |
| Status | Accepted |
| Owner | QA |
| Priority | Medium |
| Related | TestRail C49–C93 |

## Outcome

Allow a reviewer or manual tester to understand purpose, setup, usable data, execution and durable expected results directly from a TestRail case without reverse-engineering automation code or searching unrelated documents.

## Problem

Imported cases preserved traceability but were too compressed and generic for human execution. Steps did not clearly separate preparation, action, durable inspection and cleanup, and some cases lacked representative data.

## Scope

- Apply the TestRail Case Writing Standard in place to all 45 existing cases.
- Preserve case IDs, References, Automation IDs, lifecycle fields, tags and result history.
- Add stable illustrative examples only where they clarify the executable contract.
- Retain Manual, Planned, Missing/Partial and Decision-required states rather than promoting them through an editorial change.

## Acceptance criteria

1. Every case identifies owner, evidence strength, purpose, setup, material data, durable oracle, evidence to retain, bounded wait and cleanup.
2. Steps visually separate preparation, behavior, durable inspection and cleanup.
3. Generic imported actions are replaced or supplemented with case-specific variants and usable data.
4. Stable examples are included where useful and do not duplicate an already exact scenario.
5. All 45 `ESHOP-*` References and both automation metadata fields remain present.
6. Case identities C49–C93 and their Tests & Results pages remain unchanged and reachable.
7. Existing Passed history remains attached to the same cases; editing does not create test execution evidence.

## Oracle

A competent tester unfamiliar with the implementation can determine what to prepare, what to do and what to verify from the case alone, while all immutable traceability and result history remain attached to the original case identity.

## Result

Accepted after a five-case pilot and full C49–C93 rollout. UI verification confirmed 45/45 References, structured content, automation fields and result-page continuity, with no stale generic import phrasing.

## Source records

- [`../testrail-case-writing-standard.md`](../testrail-case-writing-standard.md)
- [`../evidence-baseline.md`](../evidence-baseline.md#qa-tr-01-testrail-readability-rollout)
- [`../testrail-suite-design.md`](../testrail-suite-design.md)

