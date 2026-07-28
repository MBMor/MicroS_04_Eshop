# TestRail and Traceability Governance

> **Document type:** Normative test-management, status and traceability standard  
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Version:** 1.0 
> **Status:** Draft — pending approval  
> **Effective from:** Not effective; becomes normative only after approval  
> **Last approved:** Not yet approved  
> **Last reviewed:** 2026-07-27  
> **Next scheduled review:** 2026-10-26  
> **Supersedes:** 0.1 draft  
> **Accountable owner:** QA Architecture

TestRail manages stable risk-relevant intent and reviewable metadata. Repository automation is authoritative for executable tests; append-only CI or controlled manual evidence is authoritative for execution and provenance; the [risk register](quality-risk-register.md) is authoritative for `R-*` and `CTRL-*`; the [gate policy](quality-gate-policy.md) is authoritative for `GATE-*`; and this document is authoritative for `ESHOP-*`, record types and canonical statuses.

---

## 1. Governance objectives

The model preserves stable identities, independent status dimensions, explicit material variants, first-attempt truth, immutable evidence history, rename-safe synchronization, many-to-many case/gate mappings and a clear distinction between configured intent, executable automation, execution evidence and release decisions.

A TestRail case MUST NOT be used as a mutable container for the latest build result, evidence validity, risk acceptance or gate decision.

---

## 2. Suite and intent granularity

Top-level suites: Identity/Access; Catalog; Basket; Orders; Inventory; Payments; Notifications; Gateway; Checkout; Messaging/Contracts; Persistence/Backup/Migrations; Resilience/Recovery/Reconciliation; Observability/Alerts/Health; Frontend/Accessibility; Deployment/Containers; Performance; Security/Abuse; Exploratory.

One `TestIntent` represents one risk-relevant behavior and observable outcome. Parameter rows may share one intent only when risk, failure mechanism, expected transition, authorization outcome, recovery, environment, owner and gate contribution remain equivalent. A workflow intent may span services when the distributed workflow itself is the behavior being protected.

---

## 3. Canonical status model

All dimensions are independent and exact values are mandatory.

| Dimension | Canonical values | Primary record |
|---|---|---|
| Applicability status | `Applicable`, `Not applicable`, `Unknown` | TestIntent or release-scoped mapping |
| Capability/control implementation status | `Implemented`, `Partially implemented`, `Not implemented`, `Not applicable`, `Unknown` | Risk/control assessment or TestIntent snapshot |
| Evidence strength | `Direct`, `Partial`, `Indirect`, `Missing`, `Unknown` | Variant assessment or mapping contribution |
| Execution status | `Passed`, `Failed`, `Blocked`, `Environment failed`, `Not run`, `Quarantined` | ExecutionEvidence |
| Automation status | `Automated`, `Manual`, `Planned`, `Not suitable for automation` | TestIntent/AutomationBinding |
| Oracle approval status | `Approved`, `Decision required`, `Not applicable` | TestIntent/oracle record |
| Risk acceptance status | `Not required`, `Required`, `Pending`, `Accepted`, `Expired`, `Rejected` | Risk register/decision reference |
| Gate lifecycle | `Baseline mandatory`, `Conditional mandatory`, `Advisory`, `Maturity target`, `Retired` | Gate policy; synchronized read-only |
| Gate activation | `Future`, `Active`, `Retired` | Gate policy; synchronized read-only |
| Decision phase | `Pre-merge`, `Pre-deployment`, `Post-deployment completion`, `Periodic operational` | Gate policy; synchronized read-only |
| Evidence validity | `Valid`, `Invalidated`, `Expired`, `Unknown` | ExecutionEvidence or manual-evidence record |
| Gate evaluation | `Satisfied`, `Not satisfied`, `Waived`, `Not applicable`, `Not evaluated` | GateDecision; mapping contribution excludes `Waived` |
| Deviation status | `None`, `Pending`, `Approved`, `Expired`, `Rejected` | Deviation reference |

`Passed` means the first attempt passed. Retry-only success retains first-attempt `Failed` and records retry outcome separately. An undecided required behavior is `Applicable` or `Unknown` with oracle `Decision required`; it is never converted to `Not applicable` or `Passed`.

The custom `Automation status` dimension is authoritative. TestRail's native boolean `Is Automated` is a derived compatibility field: `Automated` maps to `Yes`; `Manual`, `Planned` and `Not suitable for automation` map to `No`. Imports and later synchronization MUST update both atomically and validation MUST reject disagreement. Native `Automation Type` is outside this mapping unless a separate governed rule is approved.

---

## 4. Identifiers

- Risks: `R-<AREA>-<NNN>`.
- Controls: `CTRL-<AREA>-<NAME>-<NNN>`.
- Gates: `GATE-<AREA>-<NNN>`.
- Test intents: permanent format `ESHOP-<PREFIX>-<NNN>`.
- Case-to-gate mappings: `CGM-<NNN>`.
- Automation bindings: `AB-<NNN>` or a platform-native immutable key.
- Execution evidence: `EVID-<BUILD-OR-RUN>-<NNN>` or a protected CI/manual evidence key.
- Deviations: `DEV-<AREA>-<NNN>`.

Test type, level, priority, tier, section and automation state are fields, not parts of the `ESHOP-*` identity. Existing short IDs are therefore canonical and do not require migration. IDs are never renumbered or reused. Split creates new IDs; merge retains one survivor and retired aliases.

---

## 5. Canonical record model

### 5.1 TestIntent — stable managed case

| Field | Required content |
|---|---|
| Stable reference and title | Unique `ESHOP-*`; concise behavior and outcome |
| Risk/control IDs | Registered references or `NONE` with rationale |
| Capability/component | Single accountable owner plus responsible team |
| Test level and type | Unit, component, service integration, contract, messaging, browser, deployment, migration, resilience, performance, operational or exploratory |
| Material variants | Explicit variants the intent is expected to protect |
| Preconditions, action and oracle | Observable, approved and secret-safe |
| Applicability status | Canonical value for the capability baseline |
| Capability/control implementation status | Canonical snapshot with assessment date/source |
| Oracle approval status | Canonical value and decision reference where required |
| Default automation status | Canonical value; detailed executable links belong in bindings |
| Diagnostics and data requirements | Durable observations, safe identifiers, isolation and cleanup |
| Accountable owner | Exactly one accountable role/person |
| Lifecycle | Active, Retired or Superseded with successor/rationale |

Dynamic execution result, evidence validity, current risk acceptance, gate evaluation, waiver and release scope MUST NOT be stored as mutable scalar fields on `TestIntent`.

### 5.2 AutomationBinding — one intent to many executables

| Field | Required content |
|---|---|
| Binding ID | Immutable `AB-*` or platform key |
| TestIntent | Exactly one `ESHOP-*` |
| Executable identity | Exact xUnit/Vitest/Playwright/script/manual procedure identity |
| Framework and repository | Repository, path/project and symbol/test name |
| Covered material variants | Exact subset proven by this executable |
| Target tier | PR, Main, Nightly, Release, Post-deploy or operational |
| Environment/fidelity | In-process, Testcontainers, scripted or production-like |
| Determinism/isolation | Synchronization, clock, seed, resource ownership and cleanup |
| Binding status | Active, Planned, Retired or Superseded |

One intent may have zero, one or many bindings. A test rename updates the binding while preserving the `ESHOP-*` identity and history.

### 5.3 ExecutionEvidence — append-only result

| Field | Required content |
|---|---|
| Evidence ID | Immutable CI/manual evidence key |
| Binding and TestIntent | Resolving `AB-*` and `ESHOP-*` references |
| First-attempt execution | Canonical execution status |
| Retry metadata | Retry count and outcomes without rewriting first attempt |
| Evidence validity | Canonical value and invalidation/expiry reason |
| Material variant result | Exact executed variant and observations |
| Identity/provenance | Commit, build, image digest, schema, config, environment and timestamp |
| Diagnostics | Safe logs, traces, reports, durable state and artifact links |
| Quarantine/deviation | Separate issue/reference and expiry where applicable |
| Producer/approver | Required segregation for Critical manual/operational evidence |

Evidence is append-only. Corrections and invalidations create linked records and never overwrite the original result.

### 5.4 CaseGateMapping — many-to-many contribution

| Field | Required content |
|---|---|
| Mapping ID | Unique immutable `CGM-*` |
| TestIntent and gate | Exactly one `ESHOP-*` and one registered `GATE-*` |
| Contribution | `Primary` or `Supporting` |
| Gate requirement/material variants | Exact gate slice protected by the intent |
| Lifecycle, activation, wave and phase | Read-only synchronization from gate policy |
| Required tier | Catalog tier or explicitly stricter tier |
| Applicability | Release-scoped canonical value |
| Calendar validity profile | `None` or registered `CAL-*` profile |
| Contribution evidence strength | Canonical value for this exact gate slice |
| Latest evidence pointer | Immutable evidence ID, never the result itself |
| Contribution evaluation | `Satisfied`, `Not satisfied`, `Not applicable`, `Not evaluated` |
| Waiver reference | Blank or separate waiver ID; never fabricates satisfaction |

Examples use placeholders to avoid identifier collisions:

| Mapping | Intent | Gate | Contribution | Requirement |
|---|---|---|---|---|
| `<CGM-ID-1>` | `<ESHOP-ID>` | `<GATE-ID-1>` | Primary | concurrent duplicate safety |
| `<CGM-ID-2>` | `<ESHOP-ID>` | `<GATE-ID-2>` | Supporting | replay cardinality |

### 5.5 GateDecision — release-scope aggregate

| Field | Required content |
|---|---|
| Gate and release scope | Gate ID, candidate/deployment/period and decision phase |
| Activation/applicability | Synchronized activation and resolved scope applicability |
| Required mappings/inputs | Complete material requirement set |
| Evaluation | Canonical gate evaluation |
| Evidence set | Immutable evidence and non-test inventory references |
| Waiver | Separate eligible approved waiver or blank |
| Decision owner and timestamp | Authority defined by gate policy |

Defect, risk and vulnerability gates may use inventories rather than a single TestIntent. `Waived` exists only at this aggregate decision level.

---

## 6. Authoring templates

### 6.1 Automated TestIntent

```markdown
Reference: ESHOP-...
Title: [Capability] condition/action → observable outcome
Risk IDs: R-...
Control IDs: CTRL-...
Material variants: ...
Preconditions / action / approved durable result: ...
Diagnostics and isolation: ...
Accountable owner: ...
Oracle approval status: Approved|Decision required|Not applicable
Default automation status: Automated|Planned
```

Create one `AutomationBinding` per executable and one `CaseGateMapping` per gate contribution.

### 6.2 Controlled manual or operational evidence

Record procedure version, operator prerequisites, artifact/environment identity, bounded steps, durable result, observations, stop/rollback, retention, maximum validity, producer and independent approver.

### 6.3 Exploratory charter

Record mission, risks/gates, environment/build, safe data, observations/trace IDs, defects/new risks, follow-up evidence candidates and owner.

---

## 7. Traceability and synchronization

Minimum Critical/High chain:

```text
Risk → approved oracle → capability/route/event/state/threat
→ implementation evidence → authoritative control
→ control implementation/effectiveness → gate activation/phase
→ CaseGateMapping → TestIntent → AutomationBinding/manual procedure
→ ExecutionEvidence → first attempt and validity
→ artifact/environment identity → owners → residual/target risk
→ separate risk acceptance / gate waiver / engineering deviation
```

Source-of-truth allocation:

- repository: executable automation;
- TestRail: stable TestIntent and managed relationship metadata;
- risk register: risks, controls, scores, treatment and acceptance references;
- gate policy: gate catalog, lifecycle, activation, phase, wave and validity profiles;
- traceability matrix: current cross-artifact mappings and assessment snapshot;
- CI/manual evidence store: append-only execution/provenance;
- contract baseline registry: immutable released contracts.

Validation rejects duplicate or unresolved IDs, noncanonical values, stale bindings, missing owners, unknown gate/risk/control references, executions against retired bindings, mutable result fields on TestIntent and mappings that disagree with gate metadata.

---

## 8. Aggregation and reporting

The weakest material variant determines aggregate evidence: all Direct → Direct; mixed sufficient and insufficient → Partial; incidental only → Indirect; none → Missing; indeterminate → Unknown.

Reports keep applicability, capability/control implementation, evidence strength, automation, first-attempt execution, retries, validity, oracle approval, risk acceptance, gate lifecycle/activation/phase/evaluation, waiver and deviation separate. Pass percentage is never the sole release criterion.

Inventory units are labeled explicitly: logical test, executable case, managed TestIntent, AutomationBinding, configured, executed, first-attempt pass, retry-only pass, directly evidenced variant/risk, environment failure, blocked evidence and valid evidence.

---

## 9. Review, retirement and segregation

Retire only when behavior/risk is removed, superseded or evidenced `Not applicable`. Retain all historical intents, bindings, evidence, mappings, decisions, rationale and successors. Missing implementation, environment or owner is not retirement.

QA Architecture owns canonical schemas/enums; the area evidence owner owns TestIntents; automation teams own bindings; CI/manual evidence owners own evidence production; Product/domain authority approves business oracles; risk and gate authorities follow their governing documents.

For Critical manual/operational evidence, artifact equivalence, deviations and waivers, the sole producer/operator cannot be the sole approver.

Quarterly and pre-release review all Critical/High chains and a risk-based sample of others for unique resolving IDs, exact variants, current mappings, evidence provenance, first-attempt truth, validity, ownership, quarantine expiry and separation of acceptance, waiver and deviation.

---

## 10. Change log and approval

| Version | Date | Material change | Approved by |
|---|---|---|---|


| Role | Name | Date | Decision |
|---|---|---|---|
| QA Architecture |  |  |  |
| Engineering |  |  |  |
| Product |  |  |  |
| Security |  |  |  |
| Platform |  |  |  |
