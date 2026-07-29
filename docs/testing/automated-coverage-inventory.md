# Automated Coverage Inventory

> **Document type:** Point-in-time executable-test inventory  
> **Version:** 2.4
> **Effective from:** 2026-07-28 evidence refresh
> **Repository:** `https://github.com/MBMor/MicroS_04_Eshop`  
> **Baseline:** `main` / `b298107`; TECH-04 accepted in CI #45/#46 and TestRail R72
> **Analysis date:** 2026-07-29 (Europe/Prague)

One row is one xUnit method, Vitest `it`, or Playwright `test`. A theory is one logical test when rows prove the same risk. The accepted baseline has **193 logical tests and 198 executable cases**; five two-row theories add five executable cases. All 193 are active; none is skipped, disabled, quarantined or conditionally returned. QA-03 assigns every selector to PR, cumulative Main or Nightly runtime while Release remains an explicit overlap.

GitHub Actions PR `CI #37` accepted the reduced PR runtime, and Main `CI #38` accepted the cumulative runtime on merge commit `a41aa71`. TECH-03 passed PR `CI #41` and Main `CI #42`; TECH-04 then passed PR `CI #45` and Main `CI #46` on commit `b298107`. TestRail `R71`–`R74` retained the locked `12/22/3/4` cardinality and `ESHOP-DATA-004` passed in R72. QA-03 Nightly `R49` and Release `R50` remain the first accepted governed-tier executions. See the [executable evidence baseline](evidence-baseline.md) for provenance and limitations. The row-level assessment describes assertion scope, not a release pass; indirect risk evidence remains separate.

Risk attribution uses the 2.1 taxonomy: `R-IDENTITY-001` for token/session trust; `R-GW-AUTH-001` for gateway and addressable-service authorization; legacy `R-AUTH-001` only for the direct Catalog mutation boundary; and `R-ORDER-SEC-001` for customer order ownership.

## Summary and reconciliation

| Project / framework | Logical | Executable | Level | Active/skipped | Recommended tier |
|---|---:|---:|---|---|---|
| `Eshop.Domain.UnitTests` / xUnit v3 | 64 | 66 | Domain/application unit | 64/0 | PR |
| `ApiGateway.IntegrationTests` / xUnit v3 | 22 | 22 | Gateway integration | 22/0 | Main |
| `BasketService.IntegrationTests` / xUnit v3 | 10 | 10 | API/Redis | 10/0 | Main |
| `CatalogService.IntegrationTests` / xUnit v3 | 10 | 10 | API/PostgreSQL | 10/0 | Main |
| `InventoryService.IntegrationTests` / xUnit v3 | 17 | 17 | API/PostgreSQL | 17/0 | 14 Main; 3 Nightly + Release |
| `OrdersService.IntegrationTests` / xUnit v3 | 16 | 17 | API/PostgreSQL | 16/0 | 9 Main; 7 Nightly + Release |
| `PaymentsService.IntegrationTests` / xUnit v3 | 12 | 13 | API/PostgreSQL | 12/0 | Main |
| `NotificationsService.IntegrationTests` / xUnit v3 | 13 | 14 | API/PostgreSQL | 13/0 | Main |
| `Eshop.Messaging.IntegrationTests` / xUnit v2 | 13 | 13 | Cross-service messaging | 13/0 | 4 Main; 9 Nightly + 3 Release overlap |
| Frontend / Vitest | 13 | 13 | Component/unit | 13/0 | PR |
| E2E / Playwright | 3 | 3 | Browser workflow | 3/0 | Main |
| **Total** | **193** | **198** | 77 unit/component; 100 API; 13 messaging; 3 browser | **193/0** | **77 PR; 97 Main; 19 Nightly; 13 Release overlap** |

xUnit totals are 177 logical/182 executable, plus 13 Vitest and 3 Playwright. Source inspection found 172 Facts and five Theories. Discovery reconciled these counts with 193 unique TestRail source selectors; the mapping has 211 edges because eighteen selectors intentionally support more than one TestIntent.

## Domain and application unit tests (64 logical / 66 executable)

Inherited: `tests/backend/unit/Eshop.Domain.UnitTests`, xUnit v3, in-memory objects/fakes, no external infrastructure, current PR/main, target PR. All are Active and legacy Covered for the named behavior; concurrency/integration limitations are stated.

### Basket application (7)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `AddItemProductNotFoundReturnsNotFound` | R-BASKET-001 | NotFound; no repository write; fake Catalog/repository |
| `AddItemInactiveProductReturnsValidationFailure` | R-BASKET-001 | validation; no write; fake dependency |
| `AddItemActiveProductPersistsUpdatedBasket` | R-BASKET-001 | stored item fields; no concurrency |
| `AddItemInvalidQuantityDoesNotPersistBasket` | R-BASKET-001 | invalid and zero writes |
| `UpdateQuantityMissingItemReturnsNotFound` | R-BASKET-001 | NotFound and no write |
| `RemoveItemLastItemDeletesBasket` | R-BASKET-003 | repository delete; fake Redis |
| `ClearExistingBasketDeletesBasket` | R-BASKET-003 | customer delete; fake Redis |

### Shopping basket domain (12 logical / 13 executable)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `EmptyValidCustomerCreatesEmptyBasket` | R-BASKET-002 | normalized customer/empty items |
| `EmptyBlankCustomerThrows` | R-BASKET-002 | argument exception |
| `TryAddOrIncreaseNewItemAddsItem` | R-BASKET-001 | complete values |
| `TryAddOrIncreaseExistingItemIncreasesQuantity` | R-BASKET-001 | summed one item; sequential |
| `TryAddOrIncreaseNonPositiveQuantityFailsWithoutMutation` (`0`, `-1`) | R-BASKET-001 | two-row Theory; unchanged state |
| `TryAddOrIncreaseQuantityAboveMaximumFails` | R-BASKET-001 | >100 rejected/no mutation |
| `TryUpdateQuantityExistingItemUpdatesQuantity` | R-BASKET-001 | new quantity |
| `TryUpdateQuantityMissingItemFails` | R-BASKET-001 | false/unchanged |
| `TryUpdateQuantityInvalidQuantityFailsWithoutMutation` | R-BASKET-001 | invalid/no mutation |
| `TryRemoveExistingItemRemovesItem` | R-BASKET-001 | item removed |
| `TryRemoveMissingItemFailsWithoutMutation` | R-BASKET-001 | retained item |
| `BasketItemLineTotalMultipliesPriceAndQuantity` | R-ORDER-002 | exact multiplication; no rounding matrix |

### Catalog product domain (6 logical / 7 executable)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `CreateValidDataNormalizesValues` | R-ORDER-002 | normalized product fields/state; functional behavior, not authorization |
| `CreateEmptyIdThrows` | R-ORDER-002 | argument exception; functional behavior, not authorization |
| `CreateNonPositivePriceThrows` (`0`, `-1`) | R-ORDER-002 | two-row Theory |
| `UpdateValidDataUpdatesAndNormalizesValues` | R-ORDER-002 | updated normalized fields |
| `UpdateInvalidSkuThrowsWithoutPartialMutation` | R-ORDER-002 | original values retained |
| `DeactivateActiveProductDeactivatesProduct` | R-ORDER-002 | active false |

### Inventory domain (10)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `CreateValidDataNormalizesSkuAndCalculatesAvailability` | R-INVENTORY-001 | normalized SKU/quantities |
| `TryReserveActiveItemWithAvailableStockReservesQuantity` | R-INVENTORY-001 | reserve/availability; single-threaded |
| `TryReserveInactiveItemReturnsFalseWithoutMutation` | R-INVENTORY-001 | no mutation |
| `TryReserveInsufficientAvailableStockReturnsFalse` | R-INVENTORY-001 | no mutation |
| `UpdateOnHandBelowReservedQuantityThrowsWithoutMutation` | R-INVENTORY-001 | unchanged |
| `AdjustOnHandQuantityResultBelowReservedQuantityThrows` | R-INVENTORY-001 | unchanged |
| `AdjustOnHandQuantityValidIncreaseUpdatesAvailability` | R-INVENTORY-001 | quantities increase |
| `ReleaseReservationReservedStockReturnsItToAvailability` | R-INVENTORY-001 | release arithmetic |
| `CommitReservationReservedStockDecreasesOnHandQuantity` | R-INVENTORY-002 | commit method; unused workflow |
| `ReleaseReservationMoreThanReservedThrowsWithoutMutation` | R-INVENTORY-001 | no mutation |

### Notification domain (9)

| Test | Risk | Verified scope |
|---|---|---|
| `CreateValidDataCreatesUnreadNotification` | R-NOTIFICATION-001 | normalized unread state |
| `CreateEmptyIdThrows` | R-NOTIFICATION-001 | invalid ID |
| `CreateUnsupportedTypeThrows` | R-NOTIFICATION-001 | invalid type |
| `CreateEmptyOptionalOrderIdThrows` | R-NOTIFICATION-001 | invalid optional ID |
| `CreateBlankTitleThrows` | R-NOTIFICATION-001 | blank title |
| `CreateTitleAboveMaximumLengthThrows` | R-NOTIFICATION-001 | title limit |
| `CreateMessageAboveMaximumLengthThrows` | R-NOTIFICATION-001 | message limit |
| `MarkAsReadUnreadNotificationMarksNotificationRead` | R-NOTIFICATION-001 | flag/timestamp |
| `MarkAsReadAlreadyReadNotificationIsIdempotent` | R-MSG-001 | timestamp unchanged |

### Order domain (9)

| Test | Risk | Verified scope |
|---|---|---|
| `CreateValidItemsCreatesPendingOrderAndInitialHistory` | R-ORDER-002 | totals/items/PendingStockReservation/history |
| `CreateItemsUsingDifferentCurrenciesThrows` | R-ORDER-002 | currency invariant |
| `CreateItemBelongingToDifferentOrderThrows` | R-ORDER-002 | ownership invariant |
| `MarkStockReservedPendingOrderMovesToPendingPayment` | R-MSG-001 | transition/history |
| `MarkStockReservationFailedPendingOrderStoresNormalizedReason` | R-MSG-001 | failed state/reason/history |
| `MarkPaymentAuthorizedPendingPaymentConfirmsOrder` | R-PAYMENT-001 | Confirmed/history |
| `PaymentFailureThenCancellationRecordsBothTransitions` | R-PAYMENT-001 | PaymentFailed→Cancelled |
| `MarkPaymentAuthorizedBeforeStockReservationThrowsWithoutMutation` | R-PAYMENT-001 | invalid transition/no mutation |
| `CancelConfirmedOrderThrowsWithoutMutation` | R-PAYMENT-001 | terminal immutability |

### Fake payment processor (4)

| Test | Risk | Verified scope |
|---|---|---|
| `TryProcessSuccessMethodReturnsAuthorizedDecision` | R-PAYMENT-001 | authorized fake decision |
| `TryProcessFailureMethodReturnsFailedDecision` | R-PAYMENT-001 | failed reason |
| `TryProcessUnsupportedMethodReturnsFalse` | R-PAYMENT-001 | unsupported |
| `TryProcessBlankMethodReturnsFalse` | R-PAYMENT-001 | blank |

### Payment domain (7)

| Test | Risk | Verified scope |
|---|---|---|
| `CreatePendingValidDataNormalizesValues` | R-PAYMENT-001 | normalized Pending |
| `CreatePendingNonPositiveAmountThrows` | R-PAYMENT-001 | amount invariant |
| `AuthorizePendingPaymentMarksPaymentAuthorized` | R-PAYMENT-001 | Authorized/timestamp |
| `FailPendingPaymentNormalizesFailureReason` | R-PAYMENT-001 | Failed/reason/timestamp |
| `FailBlankReasonThrowsWithoutMutation` | R-PAYMENT-001 | Pending retained |
| `AuthorizeAlreadyAuthorizedPaymentThrows` | R-PAYMENT-001 | duplicate transition denied |
| `FailAuthorizedPaymentThrowsWithoutMutation` | R-PAYMENT-001 | unchanged Authorized |

## API Gateway integration (22)

Inherited: xUnit v3 in-process gateway, fake downstream/test auth, no containers, current PR/main, target Main.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `RootAnonymousReturnsOk` | R-DEPLOY-002 | 200/payload; in-process |
| `CatalogAnonymousForwardsRequest` | R-GW-AUTH-001 | public GET forwarded only |
| `AuthMeAnonymousReturnsUnauthorized` | R-IDENTITY-001 | 401 |
| `AuthMeAuthenticatedReturnsClaims` | R-IDENTITY-001 | subject/roles |
| `BasketAnonymousReturnsUnauthorized` | R-GW-AUTH-001 | 401/not forwarded |
| `BasketCustomerForwardsRequest` | R-GW-AUTH-001 | method/path forwarded |
| `OrdersCustomerForwardsRequest` | R-GW-AUTH-001 | method/path |
| `NotificationsCustomerForwardsRequest` | R-GW-AUTH-001 | method/path forwarding; privacy asserted separately |
| `BasketSupportReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `OrdersAdminWithoutCustomerRoleReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `InventoryCustomerReturnsForbidden` | R-GW-AUTH-001 | 403/not forwarded |
| `InventorySupportForwardsRequest` | R-GW-AUTH-001 | forwards |
| `InventoryAdminForwardsRequest` | R-GW-AUTH-001 | forwards |
| `PaymentsSupportForwardsRequest` | R-GW-AUTH-001 | forwards |
| `PaymentsAdminForwardsRequest` | R-GW-AUTH-001 | forwards |
| `CatalogAnonymousClientExceedsLimitReturnsTooManyRequests` | R-GW-001 | two 200 then 429/Problem/Retry-After; one instance/IP |
| `BasketSameCustomerExceedsLimitReturnsTooManyRequests` | R-GW-001 | subject partition 429 |
| `CheckoutSameCustomerExceedsLimitReturnsTooManyRequests` | R-GW-001 | checkout 429 |
| `CheckoutDifferentCustomersHaveIndependentLimits` | R-GW-001 | independent subjects |
| `OperationalEndpointSameUserExceedsLimitReturnsTooManyRequests` | R-GW-001 | operational 429 |
| `HealthEndpointIsNotRateLimited` | R-RESILIENCE-001 | exemption, not dependency health |
| `CrossOriginPreflightDoesNotGrantCorsAccess` | R-GW-AUTH-001 | no permissive CORS; absence policy only |

## Service API integrations (90 logical / 92 executable)

Inherited: xUnit v3, `WebApplicationFactory`, service fixtures/test auth, Testcontainers PostgreSQL or Redis, current PR/main, target Main. Health rows and three specified negative cases are legacy Partially covered; all others Covered for named variants.

### Basket (10; Redis Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; Redis-down absent |
| `BasketAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `BasketSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetBasketNewCustomerReturnsEmptyBasket` | R-BASKET-002 | empty DTO |
| `AddItemActiveProductPersistsInRedisAndIsolatesCustomers` | R-BASKET-002 | persisted and other customer empty; sequential |
| `AddItemUnknownProductReturnsNotFound` | R-BASKET-001 | 404 and empty |
| `AddItemInactiveProductReturnsBadRequest` | R-BASKET-001 | 400/no item |
| `BasketMutationFlowUpdateRemoveAndClearPersistsChanges` | R-BASKET-001 | Redis-visible sequence; no races |
| `RedisRepositorySetAndGetRoundTripsSerializedBasket` | R-BASKET-002 | serialized equivalence |
| `RedisRepositorySetAssignsExpectedAbsoluteExpiration` | R-BASKET-003 | bounded TTL; timing tolerance |

### Catalog (10; PostgreSQL Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `GetProductsDefaultReturnsOnlyActiveProducts` | R-ORDER-002 | inactive excluded |
| `GetProductsIncludeInactiveReturnsAllProducts` | R-ORDER-002 | direct functional API behavior; no authorization assertion |
| `GetProductByIdUnknownProductReturnsNotFound` | R-ORDER-002 | 404 |
| `CreateProductValidRequestPersistsNormalizedProduct` | R-AUTH-001 | 201/location/body/DB; auth unasserted |
| `CreateProductInvalidRequestReturnsBadRequest` | R-ORDER-002 | **Direct named variant:** canonical `application/problem+json` ValidationProblemDetails fields/error/trace/request IDs and unchanged PostgreSQL product cardinality; accepted in CI #46/TestRail R72 |
| `CreateProductDuplicateSkuReturnsConflict` | R-DATA-001 | 409/original retained |
| `UpdateProductValidRequestPersistsNewValues` | R-ORDER-002 | response/DB |
| `UpdateProductDuplicateSkuReturnsConflict` | R-DATA-001 | 409/original values |
| `DeleteProductExistingProductDeactivatesProduct` | R-ORDER-002 | 204/inactive |
| `DeleteProductUnknownProductReturnsNotFound` | R-ORDER-002 | 404 |

### Inventory (17; PostgreSQL Testcontainer)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `InventoryAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `InventoryCustomerUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `CreateInventoryItemSupportUserPersistsNormalizedItem` | R-INVENTORY-001 | 201/body/DB |
| `GetInventoryItemByProductIdReturnsPersistedItem` | R-INVENTORY-001 | matching DTO |
| `GetInventoryItemsDefaultQueryExcludesInactiveItems` | R-INVENTORY-001 | inactive excluded |
| `CreateInventoryItemDuplicateProductIdReturnsConflict` | R-DATA-001 | 409/one row |
| `CreateInventoryItemDuplicateNormalizedSkuReturnsConflict` | R-DATA-001 | 409/one row |
| `UpdateInventoryItemValidRequestPersistsChanges` | R-INVENTORY-001 | response/DB |
| `AdjustInventoryStockValidDeltaPersistsNewQuantity` | R-INVENTORY-001 | quantity persisted |
| `AdjustInventoryStockZeroDeltaReturnsValidationProblem` | R-INVENTORY-001 | validation Problem |
| `AdjustInventoryStockBelowReservedQuantityReturnsBadRequestWithoutMutation` | R-INVENTORY-001 | 400/unchanged |
| `MissingInventoryItemOperationsReturnNotFound` | R-INVENTORY-001 | grouped endpoints 404 |
| `ConcurrentReservationsForLastUnitDoNotOversellAndRetryLoser` | R-INVENTORY-001 | **Direct named variant:** synchronized real PostgreSQL `xmin` conflict; one reserve/one failure, no oversell, bounded retry and inbox/outbox cardinality |
| `ConcurrentMultiLineReservationsDoNotPartiallyReserveLosingOrder` | R-INVENTORY-001 | **Direct named variant:** synchronized two-line contention; losing order reserves no partial shared stock |
| `ReservationConcurrencyRetryExhaustionLeavesDatabaseUnchanged` | R-INVENTORY-001 | **Direct named variant:** deterministic three-conflict exhaustion; contextual failure and unchanged inventory/inbox/outbox |
| `InventoryRowVersionConcurrentUpdatesRejectStaleWrite` | R-INVENTORY-001 | one save/stale conflict; not competing reservation |

### Orders (16 logical / 17 executable; PostgreSQL, fake Basket)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `OrdersAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `OrdersSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `CreateOrderValidBasketPersistsOrderHistoryAndOutbox` | R-ORDER-002, R-OUTBOX-001 | first-attempt 201/order/items/history/outbox/clear |
| `CreateOrderEmptyBasketReturnsBadRequestWithoutPersistence` | R-ORDER-001 | traceable ProblemDetails; empty basket retained; zero order/item/history/outbox/idempotency/inbox rows |
| `CreateOrderMultipleCurrenciesReturnsBadRequest` | R-ORDER-002 | **Direct named variant:** traceable ProblemDetails; both currency lines retained; zero order/item/history/outbox/idempotency/inbox rows |
| `CreateOrderMissingOrMalformedIdempotencyKeyReturnsBadRequestWithoutPersistence` (missing, whitespace) | R-ORDER-001 | two-row Theory; 400 ProblemDetails and zero order/outbox/idempotency rows |
| `CreateOrderSameKeyReplaysStoredOrderWithoutReloadingChangedBasket` | R-ORDER-001 | same order/Location, 200 replay header, one durable effect and no second Basket load |
| `CreateOrderSameKeyWithChangedRequestReturnsConflictWithoutSideEffects` | R-ORDER-001 | 409 typed ProblemDetails; original cardinalities retained and Basket untouched |
| `ConcurrentIdenticalCreateOrderRequestsCreateOneOrderAndOutbox` | R-ORDER-001, R-DATA-001 | real PostgreSQL uniqueness; one 201, one 200 replay, one order/history/outbox/idempotency record |
| `SameIdempotencyKeyIsScopedToAuthenticatedCustomer` | R-ORDER-001 | identical key is independent across authenticated customers |
| `NewIdempotencyKeyUsesCurrentBasket` | R-ORDER-001 | deliberate new intent loads and persists the current basket |
| `CommittedOrderReplaysWhenBasketClearFails` | R-ORDER-001, R-BASKET-003 | durable commit survives clear exception; retry resolves original order without re-clearing |
| `GetOrdersReturnsOnlyAuthenticatedCustomersOrders` | R-ORDER-SEC-001 | owner only |
| `GetOrderOtherCustomersOrderReturnsNotFound` | R-ORDER-SEC-001 | other customer receives 404 |
| `CreateOrderInvalidEmailReturnsBadRequest` | R-ORDER-002 | **Direct named variant:** canonical ValidationProblemDetails fields/error/trace/request IDs; Basket service not called, basket retained and all Orders tables empty |

### Payments (12 logical / 13 executable; PostgreSQL)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `PaymentsAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `PaymentsCustomerUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetPaymentsOperationalRoleReturnsOk` (`Support`, `Admin`) | R-GW-AUTH-001 | two-row Theory; both 200 |
| `CreatePaymentSuccessMethodPersistsAuthorizedPayment` | R-PAYMENT-001 | 201/DB; implementation emits no event |
| `CreatePaymentFailureMethodPersistsFailedPayment` | R-PAYMENT-001 | 201/failed row/reason |
| `CreatePaymentDuplicateOrderReturnsConflict` | R-PAYMENT-001 | 409/one row; sequential |
| `CreatePaymentUnsupportedMethodReturnsBadRequestWithoutPersistence` | R-PAYMENT-001 | 400/no row |
| `CreatePaymentEmptyOrderIdReturnsValidationProblem` | R-PAYMENT-001 | ProblemDetails |
| `GetPaymentsReturnsPersistedPayments` | R-PAYMENT-001 | list |
| `GetPaymentByIdAndOrderReturnsPersistedPayment` | R-PAYMENT-001 | both query forms |
| `MissingPaymentQueriesReturnNotFound` | R-PAYMENT-001 | both 404 |

### Notifications (13 logical / 14 executable; PostgreSQL)

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `HealthAnonymousRequestReturnsOk` | R-RESILIENCE-001 | **Partial:** 200 only; DB-down absent |
| `NotificationsAnonymousRequestReturnsUnauthorized` | R-GW-AUTH-001 | 401 |
| `NotificationsSupportUserReturnsForbidden` | R-GW-AUTH-001 | 403 |
| `GetNotificationsNewCustomerReturnsEmptyCollection` | R-NOTIFICATION-001 | empty |
| `GetNotificationsReturnsOnlyCurrentCustomerInDescendingOrder` | R-NOTIFICATION-001 | ownership/order |
| `GetNotificationsUnreadOnlyReturnsOnlyUnreadNotifications` | R-NOTIFICATION-001 | unread filter |
| `GetNotificationsOrderFilterReturnsMatchingOrder` | R-NOTIFICATION-001 | order filter |
| `GetNotificationsLimitIsAppliedAfterDescendingOrdering` | R-NOTIFICATION-001 | newest limited IDs |
| `GetNotificationsInvalidLimitReturnsBadRequest` (`0`, `101`) | R-NOTIFICATION-001 | two-row Theory; 400 |
| `GetNotificationsEmptyOrderIdReturnsBadRequest` | R-NOTIFICATION-001 | validation |
| `GetUnreadCountReturnsOnlyCurrentCustomersUnreadCount` | R-NOTIFICATION-001 | exact owner count |
| `GetNotificationByIdOwnerReturnsPersistedNotification` | R-NOTIFICATION-001 | complete owner DTO |
| `GetNotificationByIdOtherCustomerReturnsNotFound` | R-NOTIFICATION-001 | other customer 404 |

## Cross-service messaging integration (13)

Inherited: xUnit v2 serialized `MessagingIntegration`, shared fixture, PostgreSQL/RabbitMQ Testcontainers, real service hosts/topology, HTTP/broker clients and bounded eventual polling. Current PR/main. The first four remain Main; the remaining nine are governed Nightly, with both duplicate-checkout variants and the Inventory concurrency variant also in the Release overlap. Timing/reset windows are determinism risks.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `InfrastructureAndServiceHostsAreAvailable` | R-DATA-001, R-DEPLOY-002 | **Partial:** service health/topology/pending migrations; dependency readiness and Catalog migration omitted |
| `CreateOrderHappyPathConfirmsOrder` | R-OUTBOX-001, R-PAYMENT-001 | order/history, stock reserved, payment, notifications, outboxes; fake Basket/no commit |
| `DuplicateCheckoutReplayCreatesOneCompleteWorkflow` | R-ORDER-001 | sequential duplicate HTTP: stable order/Location, one order/idempotency record, reservation, payment, notifications, exact outbox/inbox counts and empty queues/DLQs |
| `ConcurrentDuplicateCheckoutCreatesOneCompleteWorkflow` | R-ORDER-001 | synchronized same-basket HTTP race with one creator/replay and the same exact complete-workflow cardinality oracle |
| `ConcurrentOrderCreatedDeliveriesForLastUnitDoNotOversellOrDeadLetter` | R-INVENTORY-001 | two real Inventory hosts/consumers synchronize PostgreSQL reservation commits; exactly one workflow confirms, one stock reservation fails, no oversell, exact inbox/outbox/downstream cardinality and empty main/DLQ queues; local 3/3 plus full suite 13/13 |
| `CreateOrderWhenPaymentFailsReleasesStockAndCancelsOrder` | R-PAYMENT-001, R-INVENTORY-001 | compensation durable effects |
| `CreateOrderWithInsufficientStockMarksReservationAsFailed` | R-INVENTORY-001 | failure state/no payment/notifications/outboxes |
| `StockReservationFailedConsumerDuplicateDeliveryAppliesSideEffectsOnce` | R-MSG-001 | one consumer only |
| `StockReservedConsumerInvalidJsonDeadLettersMessageWithoutSideEffects` | R-MSG-002 | sampled queue malformed→DLQ |
| `StockReservedConsumerUnknownOrderDeadLettersMessageWithoutRetry` | R-MSG-002 | permanent prompt DLQ; timing-sensitive |
| `StockReservationFailedConsumerTransientFailureRequeuesAndProcessesMessage` | R-MSG-002 | one exception/consumer |
| `QuorumQueueDeliveryLimitExceededDeadLettersMessage` | R-MSG-002, R-MSG-003 | custom harness, not production queue |
| `OrdersOutboxRabbitMqOutageRetriesAndPublishesAfterRecovery` | R-OUTBOX-001, R-RESILIENCE-002 | Orders publisher only; environment-sensitive |

## Frontend Vitest (13)

Inherited: `src/frontend`, Vitest/jsdom/Testing Library, mocked fetch/auth, no infrastructure, current PR/main, target PR. All active and Covered for named variants.

| Test | Risk | Verified scope |
|---|---|---|
| `apiClient: adds the bearer token to an authenticated request` | R-FRONTEND-001 | Authorization header |
| `apiClient: does not add Authorization when no token exists` | R-FRONTEND-001 | header absent |
| `apiClient: preserves caller headers` | R-FRONTEND-001 | caller + auth headers |
| `apiClient: returns undefined for a 204 response` | R-FRONTEND-001 | undefined |
| `apiClient: uses problem detail for an unauthorized response` | R-FRONTEND-001 | status/message error |
| `apiClient: uses fallback message for a non-JSON forbidden response` | R-FRONTEND-001 | fallback error |
| `RequireRole: shows the sign-in state when unauthenticated` | R-IDENTITY-001, R-FRONTEND-001 | sign-in/child absent |
| `RequireRole: calls login when sign-in clicked` | R-IDENTITY-001, R-FRONTEND-001 | login called |
| `RequireRole: shows access denied when role missing` | R-IDENTITY-001 | denial/child absent |
| `RequireRole: renders children when role present` | R-IDENTITY-001 | protected child |
| `createOrder: sends the checkout idempotency key` | R-ORDER-001, R-FRONTEND-001 | exact `Idempotency-Key` header propagation |
| `CheckoutPage idempotency: reuses the key after a retryable transport failure` | R-ORDER-001, R-FRONTEND-001 | UUID retained for the same retryable submit intent |
| `CheckoutPage idempotency: creates a new key after the checkout input changes` | R-ORDER-001, R-FRONTEND-001 | changed input invalidates the previous command identity |

## Playwright browser scenarios (3)

Inherited: Chromium only, workers 1, serialized, CI retry 1, trace on first retry, screenshots/video on failure, Keycloak login, deterministic seeds; external scripted infrastructure plus seven host backend processes. Current PR/main, target Main.

| Test | Risk | Verified scope / limitation |
|---|---|---|
| `customer completes a successful checkout` | R-ORDER-001, R-PAYMENT-001 | login/basket/submit/Confirmed UI; no inventory/notification persistence |
| `order fails when inventory has insufficient stock` | R-INVENTORY-001 | StockReservationFailed/reason; long polling |
| `failed payment releases stock and cancels the order` | R-PAYMENT-001 | Cancelled/failure UI; inventory release not asserted despite title |

## Cross-cutting conclusions

- Layered overlap across domain, messaging and browser is deliberate.
- Gateway and service authorization protect distinct boundaries; Catalog direct mutation is missing.
- The nine legacy Partially covered rows are six status-only health/migration smoke rows plus Catalog invalid create and two Orders validations.
- Indirect risk evidence: trace propagation, outbox claims, inventory fulfillment, real basket-clear recovery and production ingress.
- Principal timing/flakiness sources: Redis TTL tolerance, fixed reset delays, eventual polling, browser polling, Keycloak login, Testcontainers startup and CI retry 1.
- No executable test is removed from the governed portfolio. The accepted cutover executes 77 logical/79 executable rows on PR, 174 logical/178 executable rows cumulatively on Main, 19 logical/20 executable rows on Nightly and 13 logical/14 executable Release-overlap rows.
- TECH-01 closes the direct service/DB last-unit, multiline atomicity and retry-exhaustion variants. The two-consumer broker-delivery/no-DLQ variant passed in CI #35/TestRail R46 and the governed tier runs; longitudinal scheduled history remains immature.
- TECH-02 closes the direct API/persistence and frontend key-lifecycle variants; QA-02 proves sequential and concurrent duplicate HTTP delivery produce one complete downstream workflow. The governed variants passed the first Nightly/Release runs; the five-run local concurrency smoke remains supporting determinism evidence.

## Change log

| Version | Date | Material change | Approved by |
|---|---|---|---|
| 2.4 | 2026-07-29 | Accepted TECH-04 through PR CI #45 and Main CI #46/TestRail R72 with unchanged selectors, bindings and report cardinality. | Pending review |
| 2.3 | 2026-07-29 | Added local TECH-04 Catalog `application/problem+json` evidence without changing selectors, bindings or report cardinality. | Pending review |
| 2.2 | 2026-07-29 | Accepted TECH-03 through PR CI #41 and Main CI #42/TestRail R64; closed GAP-020 with unchanged bindings and cardinality. | Pending review |
| 2.1 | 2026-07-29 | Strengthened the four ESHOP-DATA-004 selectors with traceable ProblemDetails, no-write and basket-retention evidence without changing counts or bindings. | Pending review |
| 2.0 | 2026-07-29 | Accepted PR CI #37 and cumulative Main CI #38 with TestRail R55–R58 at the locked `12/22/3/4` cardinality. | Pending review |
| 1.9 | 2026-07-29 | Recorded local cumulative PR=77/Main=174 runtime cutover and exact executable/report cardinality. | Pending review |
| 1.8 | 2026-07-29 | Promoted commit `1da2ccb` through CI #35 and accepted QA-03 Nightly R49 plus Release R50 shared execution. | Pending review |
| 1.7 | 2026-07-29 | Promoted the 193/198 GAP-001 baseline through CI #34/R41–R44 and recorded the local QA-03 tier contract. | Pending review |
| 1.6 | 2026-07-28 | Added the GAP-001 broker-delivery/no-DLQ variant and reconciled the working tree to 193/198 tests and 193 selectors/211 edges. | Pending review |
| 1.5 | 2026-07-28 | Promoted the 192/197 QA-02 baseline to shared evidence after CI #33/TestRail R37–R40 passed. | Pending review |
| 1.4 | 2026-07-28 | Added two QA-02 complete-workflow messaging variants, updated the inventory to 192/197 and separated local evidence from the accepted CI #31 baseline. | Pending review |
| 1.3 | 2026-07-28 | Recorded CI #31 and TestRail R29–R32 as shared Passed/Valid evidence for the committed 190/195 baseline. | Pending review |
| 1.2 | 2026-07-28 | Reconciled 190/195 tests and added approved TECH-02 Orders/frontend idempotency evidence and TestRail binding. | Pending review |
| 1.1 | 2026-07-28 | Reconciled 180/184 tests, recorded CI #28/TestRail evidence and added three TECH-01 Inventory concurrency variants. | Pending review |
