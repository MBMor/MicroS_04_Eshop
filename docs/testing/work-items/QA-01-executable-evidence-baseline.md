# QA-01 — Establish executable evidence baseline

| Field | Value |
|---|---|
| Type | QA governance |
| Status | Superseded as a record |
| Owner | QA |
| Priority | High |
| Completed | 2026-07-29 |

## Outcome

Give reviewers one reproducible baseline that distinguishes source inventory, local verification, shared CI results and TestRail publication without overstating any future quality gate.

## Problem

Test counts, automation bindings and evidence existed in several formats. Without a reconciled baseline, a reviewer could not reliably tell which selectors ran, which TestRail cases received results, or whether evidence was local or shared.

## Scope

- Reconcile logical tests, executable variants, TestRail TestIntents, selectors and binding edges.
- Record exact CI run, commit, environment, TestRail runs and result cardinality.
- Separate local repeat/flake smoke from immutable shared evidence.
- State residual gaps and evidence validity explicitly.

## Acceptance criteria

1. Every logical automated test has one unique source selector.
2. Deliberate selector-to-TestIntent overlaps are counted and explained.
3. Evidence records identify commit, CI run, TestRail run, tier and pass/fail/skip counts.
4. Local evidence is not presented as shared acceptance evidence.
5. Planned/manual cases receive no synthetic automated result.
6. The baseline states that no future gate is activated by the record.

## Oracle

Repository inventory, automation map, generated reports, GitHub Actions and TestRail must reconcile without an unknown selector, duplicate identity or unexplained cardinality difference.

## Result

Accepted. The continuing record moved to [`../evidence-baseline.md`](../evidence-baseline.md), currently version 3.2. This ticket is retained to explain why the evidence baseline exists; it is no longer the live baseline itself.

## Source records

- [`../evidence-baseline.md`](../evidence-baseline.md)
- [`../automated-coverage-inventory.md`](../automated-coverage-inventory.md)
- [`../traceability-matrix.md`](../traceability-matrix.md)

