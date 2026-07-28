# Quality Risk Register

> **Document type:** Authoritative risk and control registry  
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Version:** 1.5
> **Status:** Point-in-time assessed baseline — pending governance approval  
> **Effective from:** 2026-07-26 audit baseline; normative use begins only after package approval  
> **Last reviewed:** 2026-07-28
> **Next scheduled review:** 2026-10-26  
> **Accountable owner:** QA Architecture

This file is the sole authoritative registry for all `R-*` and `CTRL-*` identifiers. Other documents may reference, never independently define, controls. IDs remain after retirement with status and successor. `R-AUTH-001` remains the legacy stable ID for the Catalog mutation boundary; it is not reused for general identity or gateway authorization.

## Audit baseline and conventions

| Field | Value |
|---|---|
| Repository root | `C:\_Uceni\github\Mikroservices\MicroS_04_Eshop` |
| Branch / commit | `main` / `bf3d1afbd7bc6bbfdb7ab8994ca3ad36e51e643c` |
| Analysis date | 2026-07-26 (Europe/Prague) |
| Initial working tree | Clean (`git status --short` returned no entries) |
| Evidence convention | Repository-relative path plus symbol, configuration, job or script; stable line only when useful. |
| Legacy coverage input | Implemented, Covered, Partially covered, Indirectly covered, Documented only, Recommended, Unknown, Not applicable. |
| Canonical evidence mapping | Covered→Direct only for stated variants; Partially covered→Partial; Indirectly covered→Indirect; Recommended→Missing; Unknown→Unknown. |

Risk score is `Likelihood × Impact`: 1–4 Low, 5–9 Medium, 10–16 High, 17–25 Critical. No residual-score reduction was approved during the audit; residual scores remain equal to inherent scores. Every Critical/High reduction requires the accountable risk owner and QA Architecture to approve the exact material variants and evidence.

No tenant discriminator/model was found; tenant isolation is `Not applicable`. Customer ownership remains applicable.

## Assessed risks

Canonical values are used in status columns. Score triplets are `Likelihood / Impact / score`.

| ID | Component | Failure scenario / impact | Key implementation evidence | Inherent L/I/score | Residual L/I/score | Target L/I/score | Oracle approval | Risk acceptance status / ref |
|---|---|---|---|---|---|---|---|---|
| `R-IDENTITY-001` | Identity/token trust | Invalid lifetime, issuer, audience, signature, subject or stale session is accepted; authentication or role trust is bypassed. | shared JWT extension; Keycloak realm/client/role mapper; gateway auth tests; frontend auth provider | 3/5/15 High | 3/5/15 High | Decision required | Decision required | Required / — |
| `R-GW-AUTH-001` | Gateway/addressable-service authorization | Route, policy or non-forwarding drift permits anonymous or wrong-role access to a protected capability. | gateway routes/policies; authorization integration tests; direct service authorization tests | 3/5/15 High | 3/5/15 High | Decision required | Approved | Required / — |
| `R-AUTH-001` | Catalog service boundary | A direct caller mutates products without authentication, bypassing gateway read-only routing; product or price data is corrupted. | CatalogService/Program.cs; CatalogController; gateway appsettings.json | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-GW-001` | API Gateway throttling | Proxy/client-address trust evades, shares or inconsistently applies throttling across replicas. | GatewayRateLimitingExtensions.cs; gateway Program.cs; forwarded-header handling not observed | 3/3/9 Medium | 3/3/9 Medium | Decision required | Decision required | Not required / — |
| `R-BASKET-001` | Basket/Redis concurrency | Concurrent read-modify-write operations lose quantities or overwrite another accepted mutation. | BasketApplicationService; RedisBasketRepository whole-value Get/Set | 4/3/12 High | 4/3/12 High | Decision required | Decision required | Required / — |
| `R-BASKET-002` | Basket ownership/storage | Key or serialization contamination exposes or mutates another customer basket. | BasketKeyFactory.Create; repository; isolation integration test | 2/4/8 Medium | 2/4/8 Medium | Decision required | Approved | Not required / — |
| `R-BASKET-003` | Basket/checkout recovery | Redis loss, expiry or failed clear leaves a missing/stale basket and enables repeat checkout. | repository expiration; OrderApplicationService.CreateAsync catches clear failures | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-ORDER-001` | Checkout command processing | Retry, double-submit or concurrent delivery bypasses the approved idempotency path and creates multiple orders, outboxes or downstream payment attempts. | approved ADR 0002; OrderApplicationService.CreateAsync; `order_idempotency_records`; composite uniqueness migration; direct API/frontend tests | 4/4/16 High | 4/4/16 High | Decision required | Approved | Required / — |
| `R-ORDER-002` | Order totals/price freshness | Checkout trusts stale Redis prices or applies undefined decimal and rounding policy, producing incorrect persisted totals. | OrderApplicationService.CreateAsync; Order.Create currency and total invariants | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-ORDER-SEC-001` | Order ownership | A customer enumerates or reads another customer order through list, detail or equivalent routes. | subject predicates; owner and other-customer service integration tests | 2/5/10 High | 2/5/10 High | Decision required | Approved | Required / — |
| `R-INVENTORY-001` | Inventory reservation | Contention oversells inventory or mishandles optimistic-concurrency conflicts. | InventoryReservationService.ReserveAsync; xmin; deterministic last-unit, multiline and retry-exhaustion PostgreSQL tests | 4/5/20 Critical | 4/5/20 Critical | Decision required | Approved | Required / — |
| `R-INVENTORY-002` | Inventory lifecycle | Confirmed orders retain reserved stock because no active workflow commits, releases or ages reservations exactly once. | InventoryItem.CommitReservation; reservation service; bindings; happy path retains reservation | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-PAYMENT-001` | Payment processing | Duplicate/conflicting operational and asynchronous processing corrupts payment or order state; operational POST emits no defined result event. | PaymentRequestedConsumer; processing/application services; unique OrderId | 3/5/15 High | 3/5/15 High | Decision required | Decision required | Required / — |
| `R-MSG-001` | Consumer idempotency | Redelivery repeats state, notification, payment or inventory side effects. | inbox keys/transactions; one stock-failure duplicate test | 3/5/15 High | 3/5/15 High | Decision required | Approved | Required / — |
| `R-MSG-002` | Rabbit topology/terminal handling | Poison or permanent messages loop, drop or reach the wrong DLQ. | RabbitMqConsumerBase; topology initializer; failure-path tests | 2/4/8 Medium | 2/4/8 Medium | Decision required | Approved | Not required / — |
| `R-MSG-003` | Rabbit delivery configuration | Validated delivery limit differs from topology declaration and changes retry or DLQ behavior. | RabbitMqOptions.ConsumerDeliveryLimit; RabbitMqTopologySettings.DeliveryLimit; initializer | 3/3/9 Medium | 3/3/9 Medium | Decision required | Decision required | Not required / — |
| `R-OUTBOX-001` | Service outboxes | A committed change never publishes or is published again after a crash window. | service outboxes/workers; confirms; unique event; one Orders outage test | 3/5/15 High | 3/5/15 High | Decision required | Approved | Required / — |
| `R-OUTBOX-002` | Outbox claiming | Concurrent or stale claims strand records, steal live work or duplicate publish attempts. | SKIP LOCKED; claim owner; stale threshold in stores | 3/4/12 High | 3/4/12 High | Decision required | Approved | Required / — |
| `R-DATA-001` | PostgreSQL migration/recovery | A populated upgrade fails, rolling versions conflict or recoverable state cannot be restored. | migrations; MigrateAsync; pending smoke excludes Catalog | 3/5/15 High | 3/5/15 High | Decision required | Decision required | Required / — |
| `R-RESILIENCE-001` | Health/readiness | A service reports healthy while a mandatory dependency is unavailable. | parameterless AddHealthChecks(); status-only tests | 4/4/16 High | 4/4/16 High | Decision required | Decision required | Required / — |
| `R-RESILIENCE-002` | Rabbit recovery | Broker outage leaves provider, channel, consumer or outbox processing stalled after recovery. | automatic/topology recovery; one Orders publisher outage test | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-OBS-001` | Observability | Broken HTTP/message context prevents reconstruction; some services omit shared registration. | shared extension; Programs; message trace headers; ProblemDetails IDs | 3/3/9 Medium | 3/3/9 Medium | Decision required | Decision required | Not required / — |
| `R-DEPLOY-001` | Frontend production ingress | The built UI cannot reach APIs because nginx or production API-base routing is incomplete. | apiConfig.ts; nginx.conf; frontend Docker/Compose | 4/4/16 High | 4/4/16 High | Decision required | Decision required | Required / — |
| `R-DEPLOY-002` | Container startup/topology | Orchestration drift, early startup, partial stack or configuration mismatch prevents safe service. | Compose; E2E Compose; Bake; start script; backend absent from Compose | 3/4/12 High | 3/4/12 High | Decision required | Decision required | Required / — |
| `R-FRONTEND-001` | React/Keycloak client behavior | Refresh, session, form, loading or polling failures have undefined recovery and incomplete evidence. | auth provider; RequireRole; apiClient; pages; limited Vitest/E2E | 3/3/9 Medium | 3/3/9 Medium | Decision required | Decision required | Not required / — |
| `R-NOTIFICATION-001` | Notification ownership | A customer reads another customer notification or unread count. | authenticated subject predicates; owner and other-customer tests | 2/4/8 Medium | 2/4/8 Medium | Decision required | Approved | Not required / — |

## Risk treatment governance

One accountable risk owner is mandatory. Responsible teams and evidence owners do not share or replace accountability.

| Risk | Material controls | Accountable risk owner | Responsible treatment team(s) | Evidence owner | Target wave | Residual reduction rationale | Gate waiver ref |
|---|---|---|---|---|---|---|---|
| `R-IDENTITY-001` | CTRL-IDENTITY-TOKEN-001 | Security owner | Identity and Gateway Engineering | QA Automation owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-GW-AUTH-001` | CTRL-SEC-GATEWAY-001, CTRL-SEC-OWNERSHIP-001 | Security owner | Gateway and service Engineering | QA Automation owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-AUTH-001` | CTRL-SEC-CATALOG-BOUNDARY-001 | Catalog Engineering owner | Catalog and Platform Engineering | Catalog evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-GW-001` | CTRL-GW-RATELIMIT-001 | Gateway Engineering owner | Gateway and Platform Engineering | Gateway evidence owner | W3 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-BASKET-001` | CTRL-BASKET-CONCURRENCY-001 | Basket Engineering owner | Basket Engineering | Basket evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-BASKET-002` | CTRL-BASKET-CUSTOMERKEY-001, CTRL-SEC-OWNERSHIP-001 | Basket Engineering owner | Basket and Security Engineering | Basket evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-BASKET-003` | CTRL-BASKET-EXPIRY-001, CTRL-ORDER-IDEMPOTENCY-001 | Checkout workflow owner | Basket and Orders Engineering | Checkout evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-ORDER-001` | CTRL-ORDER-IDEMPOTENCY-001 | Orders Engineering owner | Orders Engineering | Orders evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-ORDER-002` | CTRL-ORDER-PRICE-001 | Product owner | Orders and Catalog Engineering | Orders evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-ORDER-SEC-001` | CTRL-SEC-OWNERSHIP-001 | Orders Engineering owner | Orders and Security Engineering | Orders evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-INVENTORY-001` | CTRL-DATA-CONCURRENCY-001 | Inventory Engineering owner | Inventory Engineering | Inventory evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-INVENTORY-002` | CTRL-INVENTORY-LIFECYCLE-001, CTRL-MSG-RECON-001 | Inventory Engineering owner | Inventory, Orders and Product | Inventory evidence owner | W4 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-PAYMENT-001` | CTRL-PAY-UNIQUE-001 | Payments Engineering owner | Payments and Checkout Engineering | Payments evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-MSG-001` | CTRL-MSG-INBOX-001, CTRL-MSG-REPLAY-001 | Shared Messaging owner | Consumer Engineering teams | Messaging evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-MSG-002` | CTRL-MSG-DLQ-001, CTRL-MSG-REPLAY-001 | Shared Messaging owner | Messaging and consumer Engineering | Messaging evidence owner | W3 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-MSG-003` | CTRL-MSG-DLQ-001 | Shared Messaging owner | Messaging Engineering | Messaging evidence owner | W1 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-OUTBOX-001` | CTRL-MSG-OUTBOX-001, CTRL-MSG-PUBLISH-001 | Shared Messaging owner | Orders, Inventory and Payments Engineering | Messaging evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-OUTBOX-002` | CTRL-MSG-CLAIM-001 | Shared Messaging owner | Shared Messaging and service Engineering | Messaging evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-DATA-001` | CTRL-DATA-MIGRATION-001, CTRL-DATA-BACKUP-001 | Data Migration owner | Service Engineering and Platform/DBA | Data evidence owner | W3/W4 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-RESILIENCE-001` | CTRL-OPS-READINESS-001, CTRL-OPS-ALERT-001 | Platform owner | Platform and service Engineering | Platform evidence owner | W3 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-RESILIENCE-002` | CTRL-MSG-PUBLISH-001, CTRL-OPS-ALERT-001 | Shared Messaging owner | Messaging and Platform Engineering | Messaging evidence owner | W4 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-OBS-001` | CTRL-OBS-TRACE-001, CTRL-OPS-ALERT-001 | Observability owner | Platform and service Engineering | Observability evidence owner | W4 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-DEPLOY-001` | CTRL-DEPLOY-INGRESS-001 | Frontend Engineering owner | Frontend and Platform Engineering | Deployment evidence owner | W3 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-DEPLOY-002` | CTRL-DEPLOY-ARTIFACT-001 | Platform owner | Platform and service Engineering | Deployment evidence owner | W3 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-FRONTEND-001` | CTRL-FRONTEND-SESSION-001 | Frontend Engineering owner | Frontend Engineering | Frontend evidence owner | W4 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |
| `R-NOTIFICATION-001` | CTRL-SEC-OWNERSHIP-001 | Notifications Engineering owner | Notifications and Security Engineering | Notifications evidence owner | W2 | No approved reduction; residual equals inherent until direct material-variant evidence and owner/QA approval. | — |

## Authoritative control registry

`Capability/control implementation status` and evidence strength are independent exact enums. Direct strength is limited to the named variants in the limitation column.

| Control ID | Intent | Capability/control implementation status | Evidence strength | Risks | Accountable control owner | Responsible team(s) | Current evidence / limitation |
|---|---|---|---|---|---|---|---|
| `CTRL-IDENTITY-TOKEN-001` | Validate token lifetime, issuer, audience, signature, subject, roles and supported session behavior | Partially implemented | Partial | R-IDENTITY-001 | Security owner | Identity and Gateway Engineering | Core bearer/role handling exists; full negative token/session matrix is absent. |
| `CTRL-SEC-GATEWAY-001` | Enforce gateway/addressable-service authorization and prove denial is not forwarded | Implemented | Partial | R-GW-AUTH-001 | Security owner | Gateway and service Engineering | Sample route matrix exists; exhaustive routes and equivalent direct boundaries remain incomplete. |
| `CTRL-SEC-OWNERSHIP-001` | Apply subject-based filtering to customer-owned resources | Implemented | Direct | R-BASKET-002, R-ORDER-SEC-001, R-NOTIFICATION-001 | Security owner | Basket, Orders and Notifications Engineering | Direct for sampled routes; route exhaustiveness remains separate. |
| `CTRL-SEC-CATALOG-BOUNDARY-001` | Prevent unauthorized Catalog mutation at the service or network boundary | Not implemented | Missing | R-AUTH-001 | Catalog Engineering owner | Catalog and Platform Engineering | Gateway GET-only routing is indirect and insufficient; direct boundary contract is undecided. |
| `CTRL-GW-RATELIMIT-001` | Apply subject or trusted-client fixed-window throttling | Implemented | Partial | R-GW-001 | Gateway Engineering owner | Gateway and Platform Engineering | In-process tests exist; proxy trust and multi-replica semantics are absent. |
| `CTRL-BASKET-CUSTOMERKEY-001` | Use normalized customer-scoped Redis keys and safe serialization | Implemented | Direct | R-BASKET-002 | Basket Engineering owner | Basket Engineering | Direct sampled isolation; edge/fuzz variants remain. |
| `CTRL-BASKET-CONCURRENCY-001` | Preserve concurrent basket updates under an approved merge/conflict policy | Not implemented | Missing | R-BASKET-001 | Basket Engineering owner | Basket Engineering | Concurrency oracle and atomic mechanism are not approved or implemented. |
| `CTRL-BASKET-EXPIRY-001` | Apply basket TTL and post-order clear with defined recovery | Partially implemented | Partial | R-BASKET-003 | Checkout workflow owner | Basket and Orders Engineering | TTL and best-effort clear exist; real outage/repeat-checkout behavior is absent. |
| `CTRL-ORDER-IDEMPOTENCY-001` | Create one order per logical checkout command | Implemented | Direct | R-ORDER-001, R-BASKET-003 | Orders Engineering owner | Orders Engineering | Required key, atomic persistence, replay/conflict, client lifecycle and QA-02 complete sequential/concurrent workflow passed in CI #33/TestRail R38; scheduled history remains. |
| `CTRL-ORDER-PRICE-001` | Apply an approved fresh or quoted price and decimal policy | Partially implemented | Partial | R-ORDER-002 | Product owner | Orders and Catalog Engineering | Freshness, quote expiry and rounding policy are unresolved. |
| `CTRL-DATA-CONCURRENCY-001` | Protect inventory invariants with transactional and optimistic concurrency | Implemented | Direct | R-INVENTORY-001 | Inventory Engineering owner | Inventory Engineering | Synchronized direct variants passed CI #31/TestRail R30; the two-consumer broker-delivery/no-DLQ variant passed locally 3/3 and Messaging 13/13. Shared publication and scheduled history remain. |
| `CTRL-INVENTORY-LIFECYCLE-001` | Commit, release and age inventory reservations exactly once | Partially implemented | Indirect | R-INVENTORY-002 | Inventory Engineering owner | Inventory, Orders and Product | Domain methods exist but no active complete workflow owns fulfillment/aging. |
| `CTRL-PAY-UNIQUE-001` | Produce one transactional payment decision per order across operational/asynchronous paths | Implemented | Partial | R-PAYMENT-001 | Payments Engineering owner | Payments and Checkout Engineering | Unique OrderId exists; collision/result-event semantics are unproved. |
| `CTRL-MSG-INBOX-001` | Process delivered messages idempotently with durable inbox state | Implemented | Partial | R-MSG-001 | Shared Messaging owner | Consumer Engineering teams | One consumer duplicate is directly tested; complete matrix is absent. |
| `CTRL-MSG-DLQ-001` | Classify terminal failures and route them to the intended DLQ | Implemented | Partial | R-MSG-002, R-MSG-003 | Shared Messaging owner | Messaging and consumer Engineering | Sample paths exist; full bindings/configurable-delivery contract are absent. |
| `CTRL-MSG-OUTBOX-001` | Insert business state and outbox atomically | Implemented | Partial | R-OUTBOX-001 | Shared Messaging owner | Orders, Inventory and Payments Engineering | Normal persistence exists; crash/all-publisher variants are incomplete. |
| `CTRL-MSG-CLAIM-001` | Claim outbox rows exclusively and recover stale claims | Implemented | Indirect | R-OUTBOX-002 | Shared Messaging owner | Shared Messaging and service Engineering | No direct contention, killed-owner or cleanup-race assertion exists. |
| `CTRL-MSG-PUBLISH-001` | Publish persistent mandatory messages with confirms and bounded retry | Implemented | Partial | R-OUTBOX-001, R-RESILIENCE-002 | Shared Messaging owner | Messaging and service Engineering | Orders outage is sampled; other publishers/repeated recovery are absent. |
| `CTRL-MSG-REPLAY-001` | Authorize, audit and execute idempotent replay | Unknown | Missing | R-MSG-001, R-MSG-002 | Workflow Operations owner | Workflow Operations and service Engineering | Replay support is not yet in the approved baseline. |
| `CTRL-MSG-RECON-001` | Detect and remediate stalled distributed state | Unknown | Missing | R-OUTBOX-001, R-INVENTORY-002 | Workflow Engineering owner | Workflow Engineering and Operations | Reconciliation and repair policy require an architectural decision. |
| `CTRL-DATA-MIGRATION-001` | Upgrade every supported populated schema and preserve operations | Partially implemented | Partial | R-DATA-001 | Data Migration owner | Service Engineering and Platform/DBA | Fresh migration exists; prior populated baselines/rolling window are absent. |
| `CTRL-DATA-BACKUP-001` | Restore release-critical persistent state within approved objectives | Unknown | Missing | R-DATA-001 | Platform owner | Platform/DBA | External backup configuration and restore evidence are unavailable. |
| `CTRL-OPS-READINESS-001` | Expose dependency-aware readiness separate from liveness | Not implemented | Missing | R-RESILIENCE-001 | Platform owner | Platform and service Engineering | Current health endpoints are process liveness only. |
| `CTRL-OPS-ALERT-001` | Trigger and deliver actionable alerts for release-critical failure states | Unknown | Missing | R-RESILIENCE-001, R-RESILIENCE-002, R-OBS-001 | Observability owner | Platform and service Engineering | No alert-delivery evidence exists. |
| `CTRL-OBS-TRACE-001` | Propagate/correlate end-to-end HTTP and message trace context | Partially implemented | Indirect | R-OBS-001 | Observability owner | Platform and service Engineering | No parentage assertion exists; registration is inconsistent. |
| `CTRL-DEPLOY-INGRESS-001` | Serve the built frontend and APIs through supported production ingress | Unknown | Missing | R-DEPLOY-001 | Frontend Engineering owner | Frontend and Platform Engineering | Production ingress contract/built-image API smoke are absent. |
| `CTRL-DEPLOY-ARTIFACT-001` | Start the built full stack with production-relevant configuration/runtime identity | Partially implemented | Partial | R-DEPLOY-002 | Platform owner | Platform and service Engineering | Builds exist; full backend topology/restart/readiness proof is absent. |
| `CTRL-FRONTEND-SESSION-001` | Handle authentication, session, form, loading and polling states safely | Partially implemented | Partial | R-FRONTEND-001 | Frontend Engineering owner | Frontend Engineering | Only limited component and Chromium variants exist. |

## Coverage and treatment detail

| Risk | Current aggregate evidence | Primary missing evidence / target tier |
|---|---|---|
| R-IDENTITY-001 | Partial | negative token/session/trust matrix; PR + Release |
| R-GW-AUTH-001 | Partial | route exhaustiveness, denial non-forwarding and direct boundaries; Main + Release |
| R-AUTH-001 | Missing | direct Catalog mutation authorization/deployment reachability; PR + Release |
| R-GW-001 | Partial | trusted proxy and multi-instance throttling; Release |
| R-BASKET-001 | Missing | deterministic simultaneous mutation under approved policy; Nightly |
| R-BASKET-002 | Direct | key-normalization/boundary fuzz; Main |
| R-BASKET-003 | Partial | Redis loss, real clear failure and repeat checkout; Nightly |
| R-ORDER-001 | Direct | scheduled repeat history for the shared QA-02 material variants; Nightly + Release |
| R-ORDER-002 | Partial | price policy, quote expiry and decimal/rounding matrix; Main + Release |
| R-ORDER-SEC-001 | Direct | route exhaustiveness and side-channel review; Main |
| R-INVENTORY-001 | Direct | shared publication and scheduled repeat history for the local broker-delivery/no-DLQ variant; Nightly + Release |
| R-INVENTORY-002 | Indirect | fulfillment/commit contract and aged recovery; Release |
| R-PAYMENT-001 | Partial | async duplicates, operational collision and publisher outcome; Nightly + Release |
| R-MSG-001 | Partial | all side-effecting consumers/concurrent duplicate/commit-before-ack; Nightly |
| R-MSG-002 | Partial | complete binding/DLQ/classification matrix; Main + Nightly |
| R-MSG-003 | Partial | single fixed/configured delivery-limit contract; PR |
| R-OUTBOX-001 | Partial | all publishers and publish-before-mark crash windows; Nightly + Release |
| R-OUTBOX-002 | Indirect | two publishers, killed owner, stale reclaim and cleanup race; Nightly |
| R-DATA-001 | Partial | Catalog drift, populated upgrade, rolling window and restore; Release + Operational |
| R-RESILIENCE-001 | Partial | dependency-aware readiness negative/recovery; Main + Release |
| R-RESILIENCE-002 | Partial | consumer/topology/channel recovery and repeated flap; Nightly |
| R-OBS-001 | Indirect | trace parentage, log/error linkage and complete registration; Nightly + Release |
| R-DEPLOY-001 | Missing | built-image browser through intended ingress; Main + Release |
| R-DEPLOY-002 | Partial | backend containers, dependency race, restart/runtime identity; Main + Release |
| R-FRONTEND-001 | Partial | pages, session, polling, accessibility/browser support; PR/Main/Nightly |
| R-NOTIFICATION-001 | Direct | mark-read cross-customer only if implemented; Main |

## Applicability and uncertainties

- Tenant isolation: `Not applicable`; no tenant claim, entity, key, filter or route was found. Customer ownership remains applicable.
- Inventory commit: the domain method exists, but no active path owns the complete lifecycle; oracle is `Decision required`.
- Production ingress: the repository lacks a frontend `/api` proxy and full backend Compose topology; oracle is `Decision required`.
- Secrets: fixture/default credentials exist in Keycloak and E2E configuration and are not reproduced here.
- Health: current endpoints prove process liveness, not dependency readiness.
- Risk acceptance references and target scores remain blank until accountable decisions are recorded.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 1.5 | 2026-07-28 | Recorded the local GAP-001 broker-delivery/no-DLQ proof without changing residual risk scores, acceptance or gate state. | Pending review |
| 1.4 | 2026-07-28 | Promoted QA-02 downstream idempotency proof to CI #33/TestRail R38 without changing risk scores, acceptance or gate state. | Pending review |
| 1.3 | 2026-07-28 | Recorded QA-02 local downstream idempotency proof without changing residual risk scores, acceptance or gate state. | Pending review |
| 1.2 | 2026-07-28 | Promoted TECH-01/TECH-02 direct controls to shared CI/TestRail evidence without changing residual risk scores or gate state. | Pending review |
| 1.1 | 2026-07-28 | Recorded approved TECH-02 idempotency control and direct local TECH-01/TECH-02 evidence without reducing residual risk scores. | Pending review |
