# TestRail Suite Design

> **Document type:** Repository-specific implementation design for TestRail governance  
> **Version:** 2.4
> **Status:** Implemented — the 45-case catalogue is live; latest publication is Main `CI #46` / TestRail `R71`–`R74`; docs-only `CI #47/#48` and QA-03 PR `CI #37`, Nightly `R49`, Release `R50` are accepted; TECH-05 is local
> **Effective from:** July 28, 2026 for the personal-instance CI integration; broader governance adoption remains out of scope
> **Baseline:** `main` / `06b8895`; operational CI integration introduced by `90c35bf929535fac6896a4b4606c98d87f52c0d6`
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Analysis date:** 2026-07-29 (Europe/Prague)

This design implements [TestRail and Traceability Governance](testrail-governance.md). Stable intent, executable bindings, append-only execution evidence, gate contributions and release decisions are separate records. Proposed `ESHOP-*` values are durable external references, not TestRail internal `C` IDs.

---

## 1. Project model

- Project: **Eshop Quality Engineering**
- Logical repository suite: **MicroS_04_Eshop — Product and Platform**. On the verified TestRail 10.5.1 Single Repository project, suite 7 has the fixed native name `Master` and cannot be renamed; project plus suite ID is the live identity.
- Suite mode: one hierarchical suite; runs use filters instead of cloned regression suites.
- Stable intent identity: `ESHOP-<PREFIX>-<NNN>`.
- Run identity: branch, full commit, build, environment/version, image digests, schema/config identity and test-data seed.
- Execution history: append-only CI/manual evidence; TestIntent fields are not overwritten with the latest result.

---

## 2. Section hierarchy

| Section | Repository-specific scope | Baseline status |
|---|---|---|
| 00. Governance and Test Management | audit, risks, gates, mappings, environments, defects, quarantine, evidence retention, deviations | Proposed |
| 01. Build Verification and Smoke | .NET/frontend build/static/test, liveness, topology/migration smoke, images/config | Active evidence; formal tiering future |
| 02. Identity and Authentication | Keycloak, PKCE, JWT negatives, subject/roles, refresh/logout/session | Partial |
| 03. API Gateway | routes/policies, auth-me, throttling, CORS, proxy identity, denial non-forwarding | Direct locally for route authorization; shared acceptance pending |
| 04. Catalog | public queries, mutations, validation/SKU, price contract, direct service boundary | Functional active; security boundary unresolved |
| 05. Basket | identity, Redis keys/TTL, mutations, concurrency and recovery | Concurrency/recovery future |
| 06. Orders | creation, totals, ownership, states, basket clear and idempotency | Idempotency/freshness future |
| 07. Inventory | CRUD, constraints, reserve/release/commit, contention and aging | Concurrent reservation/lifecycle future |
| 08. Payments | operational/asynchronous processing, uniqueness, duplicates, collision and outbox | Collision/recovery future |
| 09. Notifications | ownership, list/detail/unread/filter and event-driven creation | Active for current API |
| 10. Messaging and RabbitMQ | topology, headers, confirms, ack/nack, retry, DLQ, replay and recovery | Core partial |
| 11. Transactional Outbox | atomicity, claims, publish, retry, dead state, stale recovery, cleanup and crash windows | Core partial |
| 12. Checkout Saga and Compensation | happy, stock failure, payment failure, duplicates, disorder and multiline | Three canonical paths active |
| 13. Resilience and Recovery | dependency outages, flaps, restart, readiness and recovery objectives | Mostly future |
| 14. Data Integrity and Concurrency | constraints, indexes, xmin, decimals, duplicate commands, workers and Redis | Partial |
| 15. Authorization and Security | identity, gateway/service authorization, ownership, tokens, abuse and headers | Partial |
| 16. Observability | trace lineage, IDs, logs, ProblemDetails, metrics and sensitive data | Implementation partial; assertions future |
| 17. Performance and Capacity | HTTP, Redis, checkout, broker, outbox and DB contention | Future |
| 18. Containers and Deployability | Compose/Bake, non-root, topology, readiness, restart, ingress and drift | Partial |
| 19. Frontend and Browser Compatibility | components, pages, Chromium, other browsers, accessibility and session behavior | Partial |
| 20. Exploratory Testing | checkout, chaos, privacy, state abuse, operator misuse, accessibility and incident drills | Planned charters |

---

## 3. Stable references and naming

Permanent format is `ESHOP-<PREFIX>-<NNN>`. Type, priority, tier, section and automation status remain separate fields. Existing references such as `ESHOP-AUTH-001` are canonical and MUST NOT be renumbered.

Approved prefixes: AUTH, GW, CATALOG, BASKET, ORDER, INVENTORY, PAYMENT, NOTIFICATION, MSG, OUTBOX, RESILIENCE, DATA, OBS, DEPLOY, FRONTEND and E2E. New prefixes require QA Architecture approval.

Case title format:

```text
[Capability] action or condition → observable durable outcome
```

Example: `[Inventory reservation] two orders exceed availability → no oversell and one failure outcome`.

---

## 4. TestIntent fields

| Field | Rule |
|---|---|
| Stable Reference | Unique immutable `ESHOP-*` |
| Title | Risk-relevant behavior and outcome |
| Risk IDs | Registered `R-*`; `NONE` requires rationale |
| Control IDs | Registered `CTRL-*` from the risk register only |
| Component | Identity, Gateway, Catalog, Basket, Orders, Inventory, Payments, Notifications, Messaging, Outbox, Checkout, Data, Observability, Deployment, Frontend, Platform or Cross-service |
| Test Level | Unit, Component, API integration, Persistence integration, Contract, Messaging integration, Browser E2E, Deployment, Security, Migration, Resilience, Performance, Operational or Exploratory |
| Primary Type | Functional, Regression, Smoke, Security, Integration, Resilience, Recovery, Migration, Performance, Accessibility, Compatibility or Exploratory |
| Priority | Critical, High, Medium or Low; never a substitute for risk score |
| Material Variants | Explicit scope protected by the intent |
| Preconditions/Data/Action/Oracle | Observable, approved, bounded and secret-safe |
| Applicability Status | Applicable, Not applicable or Unknown |
| Capability/control implementation status | Implemented, Partially implemented, Not implemented, Not applicable or Unknown |
| Oracle approval status | Approved, Decision required or Not applicable |
| Default automation status | Automated, Manual, Planned or Not suitable for automation |
| Accountable Owner | Exactly one accountable role/person |
| Responsible Team | One or more execution teams |
| Diagnostics/Isolation | Required identifiers, resources, cleanup, clocks, synchronization and safe logs |
| Lifecycle | Active, Retired or Superseded with successor/rationale |

Do not add mutable scalar fields for latest execution, evidence validity, risk acceptance, gate significance/evaluation, waiver or release candidate.

Custom `Automation Status` is the source of truth. Native `Is Automated` is derived as `Yes` only for `Automated`, otherwise `No`; validation rejects mismatches. Native `Automation Type` remains unmanaged until a separate mapping is approved.

---

## 5. Relationship and evidence entities

### 5.1 AutomationBinding

| Field | Rule |
|---|---|
| Binding ID | Immutable `AB-*` or platform key |
| Stable Reference | Exactly one `ESHOP-*` |
| Executable Identity | Exact xUnit/Vitest/Playwright/script/manual procedure identity |
| Repository Reference | Relative path, project and symbol/test |
| Material Variant Subset | Exact variants proved by this binding |
| Framework/Level | Framework and executable test level |
| Target Tier | PR, Main, Nightly, Release, Post-deploy or Operational |
| Environment/Fidelity | In-process, Testcontainers, scripted or production-like |
| Determinism/Isolation | Synchronization, clock, seed, resource ownership and cleanup |
| Status | Active, Planned, Retired or Superseded |

### 5.2 ExecutionEvidence

| Field | Rule |
|---|---|
| Evidence ID | Immutable protected CI/manual key |
| Binding/TestIntent | Resolving `AB-*` and `ESHOP-*` |
| First Attempt | Passed, Failed, Blocked, Environment failed, Not run or Quarantined |
| Retry Metadata | Separate attempts/results; never rewrites first attempt |
| Evidence Validity | Valid, Invalidated, Expired or Unknown |
| Executed Variant | Exact material variant and observations |
| Evidence Identity | Commit, build, digest, schema, config, environment and time |
| Diagnostics | Safe report/log/trace/state links |
| Quarantine/Deviation | Separate issue/expiry and `DEV-*` reference |
| Producer/Approver | Independent approval where required |

### 5.3 CaseGateMapping

| Field | Rule |
|---|---|
| Mapping ID | Unique immutable `CGM-*` |
| Stable Reference | Exactly one `ESHOP-*` |
| Gate ID | Exactly one registered `GATE-*` |
| Contribution | Primary or Supporting |
| Requirement/Material Variant | Exact protected gate slice |
| Lifecycle/Activation/Wave/Phase | Read-only synchronized from gate policy |
| Required Tier | Gate catalog tier or stricter mapping tier |
| Applicability | Applicable, Not applicable or Unknown for release scope |
| Calendar Profile | None or registered `CAL-*` profile |
| Contribution Evidence Strength | Direct, Partial, Indirect, Missing or Unknown |
| Latest Evidence Pointer | Immutable `EVID-*`/CI reference or blank |
| Contribution Evaluation | Satisfied, Not satisfied, Not applicable or Not evaluated |
| Waiver Reference | Separate waiver ID or blank |

### 5.4 GateDecision

Stores release scope, activation/applicability, complete required mapping/input set, aggregate evaluation, immutable evidence set, waiver, decision owner and timestamp. `Waived` is available only here, not on TestIntent or a single mapping contribution.

---

## 6. Authoring and automation rules

- Preconditions identify roles/subject, states, dependencies, schema, queue/outbox and unique seed.
- Secrets, passwords, tokens, connection strings and keys are never copied into cases or evidence.
- Expected results cover HTTP/ProblemDetails, durable state/history, cardinality, totals, stock/payment/notification, inbox/outbox, ack/nack/DLQ, diagnostics and absence of partial state where applicable.
- Eventual workflows name terminal business conditions and bounded polling; arbitrary sleeps and reruns never define success.
- Shared database, RabbitMQ, Redis, topology, time and fault mutations require dedicated or serialized resources and scoped cleanup.
- `Automated` requires at least one active AutomationBinding. `Planned` never counts as coverage.
- A binding may prove only the variants explicitly recorded on it; one representative consumer does not prove another consumer with different state or side effects.

---

## 7. Run design and target tiers

| Run | Intended contents |
|---|---|
| PR qualification | build/static, unit/component, selected fast service/gateway, contract and schema/ID validation |
| Main integration | full service integration, canonical messaging, built-image smoke and critical Chromium |
| Nightly resilience | deterministic concurrency, retries/DLQ, stale claims/crash, recovery, performance smoke, cross-browser and flake detection |
| Release candidate | immutable full-stack artifacts, populated migration, security/token, ingress, readiness/recovery, due calendar evidence, performance/accessibility/exploration |
| Post-deployment completion | target digest/schema/config, ingress synthetic, health and telemetry |
| Periodic operational | restore, alerts, runbooks, recovery objectives and other calendar-profile evidence |

All 194 local logical tests remain governed. QA-03 defines authoritative primary ownership `PR=77`, `Main=98`, `Nightly=19` plus `Release=13 overlap`; Main is cumulative at 175 selectors. Nightly `R49` and Release `R50` accepted shared execution/publication. PR `CI #37` and Main `CI #38` accepted the earlier cutover and GAP-022 is complete. TECH-05 changes counts only after that acceptance baseline and still needs its own shared PR/Main evidence.

---

## 8. Review and segregation of duties

1. Author creates or updates TestIntent, risks/controls, oracle, material variants and proposed mappings.
2. Component/domain owner validates behavior; Security, Platform or Data review their domain.
3. QA/SDET reviews determinism, fidelity, diagnostics, isolation, duplication and ID uniqueness.
4. Automation owner creates bindings; CI/manual evidence is imported append-only.
5. Critical/High and gate-contributing intents require QA governance review.
6. Critical manual/operational evidence, artifact equivalence, deviations and waivers cannot be approved solely by their producer/operator.
7. Failure preserves evidence and defect linkage. Quarantine is limited to 14 days unless explicitly extended under policy and cannot satisfy a mandatory gate.
8. Retirement preserves ID, reason, replacement, last binding/evidence and approval.

Quarterly review covers all Critical/High chains and a risk-based sample of other intents for evidence paths, identities, stale bindings, canonical values, secret hygiene, mapping completeness, quarantine and ownership.

---

## 9. Reporting and implementation prerequisites

Reports separate:

- configured TestIntents and AutomationBindings;
- first-attempt result and retry-only outcome;
- evidence validity and age/profile;
- capability/control implementation and evidence strength;
- oracle approval and risk acceptance;
- gate lifecycle, activation, wave, phase, contribution and aggregate evaluation;
- waiver/deviation and expiry;
- durations, data/resource leakage and Critical/High gaps.

Before live synchronization, implement a version-controlled registry/manifest and validation that rejects duplicate IDs, unresolved references, noncanonical enum values, mutable result fields on TestIntent, stale executable bindings and gate metadata that differs from the gate policy.

---

## 10. Import catalogue baseline

The original import catalogue and its reviewable representation are intentionally archived outside this repository. Import version 1.1 contained 45 TestIntents and 184 exact AutomationBindings for 177 logical checked-in tests; seven additional bindings intentionally associated the same executable with a distinct material subset of another TestIntent.

The repository runtime contract is [`scripts/testrail/automation-id-map.json`](../../scripts/testrail/automation-id-map.json). The local TECH-05 state contains 31 automated TestIntents, 194 unique source selectors and 212 binding edges. The two QA-02 messaging selectors bind to both `ESHOP-ORDER-002` and `ESHOP-E2E-001`; the GAP-001 broker selector binds to both `ESHOP-DATA-002` and `ESHOP-INVENTORY-002`. Merge commit `b298107` passed GitHub Actions `CI #46`; TestRail `R71`–`R74` remain the latest closed 100% publication with `12/22/3/4` results. Docs-only CI #47/#48 correctly created no run. The governed subsets previously published Nightly `R49` with 11/11 and Release `R50` with 6/6 Passed without creating a case.

TECH-03/GAP-020 strengthens all four existing `ESHOP-DATA-004` bindings in place. Selector count, mapping edges, Automation ID, Main ownership and the Backend Integration aggregate count of 22 did not change. PR CI #41 and Main CI #42 passed; `[Negative mutations]` passed in TestRail R64 and GAP-020 is closed.

TECH-04 adds an exact `application/problem+json` assertion to the existing Catalog binding and changes only shared response serialization. PR CI #45 and Main CI #46 passed; `[Negative mutations]` passed in TestRail R72 without adding a selector, binding, case or report row.

TECH-05 adds one Main selector with 43 runtime rows to existing `ESHOP-GW-001`. The authoritative registry covers 13 YARP routes and 3 local endpoints and the validator rejects configuration drift. The catalogue remains 45 cases and Main publication remains `12/22/3/4`; the next accepted Main run must update the existing gateway TestIntent rather than create a case. No Future gate is activated.

The 36 identifiers already present in traceability-matrix.md are preserved without renumbering or semantic reuse. Nine materially distinct additions are ESHOP-AUTH-002, ESHOP-BASKET-004, ESHOP-MSG-004, ESHOP-E2E-004, ESHOP-RESILIENCE-004, ESHOP-DATA-003, ESHOP-DATA-004, ESHOP-OBS-002 and ESHOP-DEPLOY-003. They cover security-header/origin policy, sequential basket behavior, replay/reconciliation, exploratory failure discovery, capacity, restore, atomic negative contracts, alert delivery and post-deployment completion. These additions remain Proposed and do not activate any gate.

The archived canonical CSV uses exactly 28 source columns and a repeated step-row model. Generated upload transports add a 29th, derived `Is Automated` column. Evidence Strength, owner, test data, final durable oracle, diagnostics and cleanup remain explicit in Preconditions because the mandated source column set has no dedicated fields. On the verified instance, canonical Type `Smoke` maps explicitly to TestRail `Smoke & Sanity`; this transport adaptation does not change the canonical enum.

The browser-UI import was completed in the personal evaluation instance on 2026-07-27. Project 3 / single repository suite 7 contains all 21 roots, 66 total sections and 45 cases. A final export reconciled 186 step rows and 45 unique References with zero title, hierarchy, Type, step-count or automation-field drift. Eleven missing instance Type values and four missing Component values were added without changing the canonical catalogue. This proves import mechanics only; it is not execution evidence, oracle approval, risk acceptance, gate activation or governance approval.

---

## 11. Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 2.4 | 2026-07-29 | Recorded docs-only CI #47/#48 acceptance and local TECH-05 binding of the complete gateway matrix to existing ESHOP-GW-001; shared acceptance remains pending. | Pending review |
| 2.3 | 2026-07-29 | Accepted TECH-04 through CI #45/#46 and TestRail R72 without catalogue, binding or report-cardinality change. | Pending review |
| 2.2 | 2026-07-29 | Recorded local TECH-04 media-type evidence without catalogue, binding or report-cardinality change. | Pending review |
| 2.1 | 2026-07-29 | Accepted TECH-03 through CI #41/#42 and TestRail R64; closed GAP-020 without catalogue or cardinality change. | Pending review |
| 2.0 | 2026-07-29 | Recorded local TECH-03/GAP-020 strengthening of the existing ESHOP-DATA-004 bindings without catalogue or cardinality change. | Pending review |
| 1.9 | 2026-07-29 | Accepted PR CI #37 and cumulative Main CI #38 with TestRail R55–R58; closed GAP-022. | Pending review |
| 1.8 | 2026-07-29 | Implemented the cumulative PR/Main execution contract locally with fail-closed selector and TestRail report cardinality. | Pending review |
| 1.7 | 2026-07-29 | Promoted the committed QA-03 contract through CI #35 and accepted Nightly R49 plus Release R50 publication. | Pending review |
| 1.6 | 2026-07-29 | Promoted the 193-selector contract through CI #34/R41–R44 and recorded the local QA-03 tier implementation. | Pending review |
| 1.5 | 2026-07-28 | Added the local GAP-001 binding and reconciled the runtime contract to 193 selectors/211 edges while retaining CI #33 as the shared baseline. | Pending review |
| 1.4 | 2026-07-28 | Recorded CI #33/TestRail R37–R40 acceptance for QA-02 and synchronized C60 messaging evidence metadata. | Pending review |
| 1.3 | 2026-07-28 | Added QA-02 runtime bindings and recorded the 192-selector/209-edge working tree separately from the accepted CI #31 baseline. | Pending review |
| 1.2 | 2026-07-28 | Recorded CI #31/TestRail R29–R32 acceptance for the committed 31-intent TECH-02 mapping and C60 shared evidence. | Pending review |
| 1.1 | 2026-07-28 | Recorded approved TECH-02 automation, current 31-intent runtime-map reconciliation and external archival of import-only artefacts. | Pending review |
