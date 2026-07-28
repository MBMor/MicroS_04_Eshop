# Test Engineering Standards

> **Document type:** Normative evidence-design and automation standard  
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Version:** 1.0  
> **Status:** Draft — pending approval  
> **Effective from:** Not effective; becomes normative only after approval  
> **Last approved:** Not yet approved  
> **Last reviewed:** 2026-07-26  
> **Next scheduled review:** 2026-10-26  
> **Supersedes:** 0.1 draft  
> **Accountable owner:** QA Architecture

The [strategy](test-strategy.md) governs principles and document precedence; the [gate policy](quality-gate-policy.md) governs blocking, phases, evidence validity and waivers; [TestRail Governance](testrail-governance.md) governs metadata. The [risk register](quality-risk-register.md) is the sole authoritative control registry.

---

## 1. General evidence standard

Evidence MUST be deterministic, repeatable, isolated, bounded, diagnostic, traceable to an approved oracle and material variants, owned, executed at the lowest sufficient layer and faithful to relevant production behavior.

Assertions SHOULD cover durable state, valid transitions, cardinality, contracts/routing, ack/retry, customer outcome, diagnostics and recovery. Negative cases assert absence of partial state and unintended messages. Failure output includes safe customer/order/event/message/trace/queue/outbox/inbox/image/schema identifiers.

Tests MUST change when approved behavior changes and MUST NOT be weakened to make CI pass. Retirement requires removal/replacement of protected behavior and accountable approval.

The exact implementation field name is **Capability/control implementation status**. It describes the product capability/control, never test implementation. Test implementation is represented by `Automation status`.

### 1.1 Evidence-record separation

A stable `TestIntent` defines behavior, oracle, material variants and ownership. One or more `AutomationBinding` records identify executable tests or controlled procedures. Every run produces append-only `ExecutionEvidence` with first-attempt status, retries, validity and immutable provenance. `CaseGateMapping` records gate contributions; `GateDecision` records aggregate release evaluation.

Tests and TestIntents MUST NOT overwrite historical execution evidence or store a mutable latest gate decision, risk acceptance or waiver as if it were intrinsic test metadata. An automation rename updates its binding without changing the stable `ESHOP-*` intent.

---

## 2. Levels and production fidelity

| Level | Primary use | Required fidelity |
|---|---|---|
| Unit/component | transitions, calculations, policies, frontend states, timing classification | no real network/shared DB; fast and parallel-safe |
| Service integration | middleware, auth, EF, Redis, gateway, health | real engine where constraints/transactions/atomicity/TTL matter |
| Contract | HTTP/event/schema/routing/version compatibility | generated current contract versus approved immutable baseline |
| Messaging/workflow | outbox/inbox, broker, crash, duplicate, compensation, replay | real PostgreSQL and RabbitMQ |
| Browser | auth, customer journeys, routing, polling, accessibility | production-relevant ingress/auth; few and uniquely seeded |
| Deployment/migration/resilience/performance | artifacts, network, filesystem, upgrades, outage, load | isolated production-like environment |
| Operational | alerts, restore, runbooks, post-deploy | approved procedure, identity, validity and independent approval where Critical |

High-level tests do not duplicate low-level permutations unless integration is the risk.

---

## 3. API, gateway and authorization

For each endpoint select applicable role/identity, anonymous/wrong-role, ownership/substitution, request/response contract, boundaries, missing resource, concurrency/idempotency, ProblemDetails, durable/no-partial state, downstream invocation/non-invocation, correlation, degradation and abuse/rate limits.

Gateway denial MUST prove no forwarding. Direct service evidence preserves defense in depth; network-delegated authorization requires approved topology and direct network evidence.

Token negatives include expiry/not-yet-valid, issuer/audience, signature/algorithm, subject, roles, session/refresh, clock skew, malformed structures and replay where required. Ownership covers route completeness, 404/403 policy, admin/support, recovery authorization and equivalent-route differentials.

---

## 4. Persistence, migration and restoration

Direct persistence tests cover keys, constraints, cascade/restrict, precision/scale, concurrency tokens, state/query indexes, outbox/inbox uniqueness, atomicity, reconciliation queries and cleanup eligibility. Database-level Critical/High invariants require direct bypass tests.

Migration evidence covers zero creation; every supported populated prior schema; data/history preservation; constraints/indexes; post-upgrade reads/writes; rollback/forward-fix; rolling old/new compatibility; drift detection; representative volume; and deployed schema identity. Fresh databases alone are insufficient.

Restore evidence covers isolated restoration; schema/history; critical records; writes; outbox/inbox; duplicate/replay; broker consistency and rebuilt/empty Redis; measured recovery point/duration; RTO/RPO. PITR adds declared point, cross-database state, messages around the point and reconciliation. Restart is not restore evidence.

---

## 5. Contract compatibility and baselines

The pipeline generates current OpenAPI/event schemas and compares them to the immutable released baseline governed by [Quality Gate Policy section 6](quality-gate-policy.md#6-contract-baseline-governance). It classifies additive, behavioral and breaking changes; fails unapproved removal/type/required-field/status changes; verifies gateway route/auth allowlists; retains provenance-linked diffs; and links exceptions.

Each event defines type/version, routing key, fields, naming/enums, identifiers/time, correlation/trace, compatibility, replay and owner. Consumers tolerate unknown additive fields unless policy says otherwise; removal/reinterpretation within a major version is prohibited; new required fields need version/bridge; enum changes are explicit; routing changes are topology breaking; semantic change requires versioning.

Broker topology drift checks exchanges, durability, queues/types, bindings, routing, DLX, delivery limit, TTL and policies against the approved contract.

---

## 6. Messaging reliability

Control references resolve only to [Quality Risk Register](quality-risk-register.md#authoritative-control-registry).

### 6.1 Outbox matrix

For every material publisher: atomic business/outbox insert; eligibility/ordering; disjoint concurrent claims; live-owner protection; stale reclaim; persistent mandatory confirmed publish; unroutable detection; bounded retry; mark after success; publish-before-mark crash; observable exhaustion; eligible cleanup; bounded shutdown; backlog/terminal metrics.

### 6.2 Consumer matrix

| Variant | Required observations |
|---|---|
| First valid delivery | domain/inbox/outbox and ack |
| Sequential duplicate | unchanged logical cardinality and duplicate classification |
| Concurrent duplicate | one logical side effect under synchronized delivery |
| Transient dependency/concurrency | bounded retry, no partial state, diagnostics |
| Permanent business | terminal classification and ack/DLQ policy |
| Malformed/unknown/incompatible | no mutation, exact terminal path and safe diagnostics |
| Late/out-of-order | approved transition, terminal immutability |
| Duplicate after restart | persistent idempotency |
| Failure before inbox commit | safe redelivery and eventual single result |
| Business commit before ack | safe duplicate handling |
| Delivery exhaustion | exact DLQ reason/location and signal |

No representative consumer proves another consumer with different storage, transition or side effects.

### 6.3 Classification, replay and reconciliation

Failures are transient technical, concurrency transient, permanent business, malformed, configuration/topology, compatibility or unknown. Policy defines attempts/backoff/DLQ/terminal state/diagnostics/replay eligibility.

Supported replay is authorized, auditable, rate-bounded, compatible and idempotent; it tests single/batch, duplicate, restart, contract change, repeat failure/loop prevention, cardinality and audit trail.

Critical async workflows have a detective mechanism for aged non-terminal orders, orphan reservation/payment/event/notification state, DLQ blockage, stale inbox/outbox and restore mismatch. Evidence covers accuracy, false positives, diagnostics, ownership, remediation and repair idempotency.

Use injected `TimeProvider`, durable state, confirms, queue state and bounded diagnostic polling. Wall-clock sleeps require a documented standard deviation if used systematically for normative evidence.

---

## 7. Concurrency and commercial integrity

Inventory tests synchronize simultaneous limited-stock reservations; multi-line atomicity; reserve/release race; duplicate/replay; retry success/exhaustion; crash/recovery. Assert `OnHand >= 0`, `Reserved >= 0`, `Reserved <= OnHand`, accepted quantity within availability, one result/order, idempotent release and observable exhaustion.

Orders cover duplicate command, repeated/conflicting results, exact decimals, stale update, terminal immutability, idempotency, compensation/success race and replay after terminal state.

Payments cover simultaneous requests, duplicate delivery, partial retry, unique order/payment relationship, one logical result, late conflicts, replay, restore/reconciliation.

Basket concurrency is `Blocked` with oracle `Decision required` until last-write-wins, version conflict, merge or atomic mutation is approved.

---

## 8. Security and supply chain

Browser/ingress evidence covers PKCE, redirects, expiry, CORS, forwarded-header trust, external rate-limit identity, header ownership, development bypass, spoofing and production API base. Artifact inspection excludes secrets/tokens/customer/payment payloads and development credentials; ProblemDetails hides internals; DLQ/replay/backup follow access/retention.

The pipeline SHOULD include NuGet/npm/image/IaC audit, secret scan, SAST, isolated authenticated DAST, API fuzzing, SBOM, license policy, base-image digest policy and reachable Critical triage. Runtime tests SHOULD cover non-root, writable paths, read-only root, dropped capabilities, bounded resources, network exposure, secure fail-fast and absence of dev tooling.

Fuzzing is bounded, isolated and retains reproducible inputs.

---

## 9. Frontend, browser, accessibility and compatibility

Major components cover loading, empty, success, validation, disabled submission, denial, recoverable/terminal error, retry, cancellation/unmount, polling stop, session expiry and stale responses using controlled timers.

Main carries a small serialized Chromium critical path; Nightly carries approved Firefox/WebKit/mobile coverage; Release carries applicable accessibility/compatibility acceptance. WCAG 2.2 AA is the target unless another baseline is approved. Automation does not replace keyboard/screen-reader/manual checks.

The support matrix identifies exact browsers/viewports, Node, .NET, PostgreSQL, Redis, RabbitMQ, container platform and architecture. Missing required scope is `Decision required`.

---

## 10. Observability, health and alerts

At least one successful and failed critical workflow proves connected ingress/HTTP/publish/consume trace context; stable service names; correlation/event/message IDs; business spans; failure status; ProblemDetails/log identity; retry/DLQ/replay/outbox signals; safe data; throughput/failure/backlog/age metrics.

Avoid exact total span count, unstable internal spans, exact log wording and vendor fields unless contractual.

Release-critical alerts prove trigger, intended delivery, actionable context, deduplication, resolution, owned runbook and valid dashboard query. A configured alert is not proven.

Liveness means process running; readiness means capable of intended traffic with mandatory dependencies; startup may be separate. Checks are bounded, non-destructive and diagnostic.

---

## 11. Resilience and recovery

For PostgreSQL, Redis, RabbitMQ, Keycloak, downstream services and mandatory configuration/telemetry, stop/isolate and assert readiness, liveness contract, bounded customer behavior, no partial state, policy retry/rejection, diagnostics/alert, recovery and backlog drain without duplicates.

Crash points: business commit before publish; publish before mark; consumer commit before ack; held claim; compensation; shutdown; replay/reconciliation; migration where supported. Faults are injected at observable boundaries.

DR exercises restore databases, handle broker at a different point and rebuilt Redis, reconcile, verify critical workflows and measure time/data loss. Destructive exercises only in isolated environments. Runbooks specify prerequisites, permissions, target safety, stop/rollback, diagnostics and durable result.

---

## 12. Performance, deployment and post-deploy

Performance requires approved workload/SLOs. Report p50/p95/p99, error, throughput, saturation, locks/connections, queue/outbox age, saga/reconciliation/replay/recovery time and resource limits. A valid run records topology, dataset, warmup, duration, arrival/concurrency, limits, thresholds, digests, config, schema and broker state.

Built-image evidence covers configuration fail-fast, UID/filesystem, health/shutdown, architecture, deployment DNS, frontend ingress, persistence, exposure, digest and no dev config. Cleanup targets only isolated resources created by the run.

Post-deploy verifies digest/config/schema, liveness/readiness, ingress, safe synthetic, trace, latency/error, outbox/backlog/DLQ and warnings. It has success, observation window, rollback/forward-fix triggers, owner and retained evidence.

Rollback proves previous artifact, app/schema/data/message compatibility and signals. If unsafe, forward-fix is documented and exercised.

---

## 13. Data, isolation and waiting

Generate unique IDs/SKUs/subjects/message IDs; use controlled clocks/seeds; tests own data and order independence; no production personal data/secrets. Dedicated databases/schemas, queues, Redis prefixes, browser users and performance/recovery infrastructure are required. Parallelism follows proven isolation.

Replace sleeps with readiness, durable row/queue/UI/telemetry/alert/reconciliation conditions. All waits are bounded and diagnostic.

---

## 14. Engineering-standard deviation and review

When an alternative provides equivalent or greater evidence, create a `DEV-*` record under [strategy section 12](test-strategy.md#12-engineering-standard-deviations). A deviation cannot manufacture a pass, change a gate lifecycle or replace a waiver.

Reviewers confirm risk/oracle/variants, sufficient level/fidelity, determinism, durable/cardinality assertions, diagnostics/redaction, isolation, stable IDs/mappings, provenance, owner/tier/phase, and no weakened behavior.

Critical manual/operational evidence and deviations require approval by an accountable person other than the sole operator/producer.

---

## 15. Change log and approval

| Version | Date | Material change | Approved by |
|---|---|---|---|


| Role | Name | Date | Decision |
|---|---|---|---|
| QA Architecture |  |  |  |
| Engineering |  |  |  |
| Product |  |  |  |
| Security |  |  |  |
| Platform |  |  |  |

