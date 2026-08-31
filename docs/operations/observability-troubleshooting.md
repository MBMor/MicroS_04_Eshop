# Observability Troubleshooting Runbook

This runbook describes how to investigate an e-shop workflow across services using the Aspire Dashboard and OpenTelemetry traces.

## Scope

Use this workflow for local operational troubleshooting of:

- API Gateway requests
- Orders processing
- Inventory processing
- Payments processing
- RabbitMQ message publishing and consumption
- cross-service latency and failures
- business correlation through Order ID, Payment ID, and messaging correlation ID

The Operations Console does not query telemetry directly.

It provides business identifiers and a hand-off to the Aspire Dashboard, where distributed traces are inspected.

## Useful telemetry tags

The application enriches relevant spans with:

```text
eshop.order.id
eshop.payment.id
eshop.correlation_id
```

`eshop.order.id` is the primary business identifier for following one checkout workflow across Orders, Inventory, and Payments.

`eshop.payment.id` identifies the Payment entity created for an order.

`eshop.correlation_id` identifies the messaging correlation context carried through RabbitMQ processing.

## Prerequisites

Start the application through the local Aspire development environment and verify that the Aspire Dashboard receives traces.

The Operations Console Diagnostics screen should show a configured Aspire Dashboard URL.

## Scenario: trace a successful checkout

### 1. Create an order

Use the web frontend to:

1. sign in as a customer
2. add a product to the basket
3. complete checkout

Wait until the order reaches its expected final state.

### 2. Obtain the Order ID

Open the Operations Console with a support or admin account.

Open:

```text
Orders
```

Refresh the list and select the newly created order.

Copy the complete Order ID.

Example:

```text
29ae3072-1111-2222-3333-444444444444
```

Do not use only the shortened ID shown in a troubleshooting context badge.

### 3. Open Aspire

In the Operations Console open:

```text
Diagnostics
› Observability
› Open Aspire dashboard
```

Open the distributed traces view.

### 4. Find the checkout trace

Look for the trace whose root HTTP request is similar to:

```text
api-gateway: POST /api/v{version}/orders
```

The trace should contain spans from multiple resources, for example:

```text
api-gateway
orders-service
basket-service
inventory-service
payments-service
```

The exact number of spans may vary.

The important point is that they belong to one distributed trace.

### 5. Inspect the Orders span

Open the Orders span related to creating the order.

Find the span attributes.

Verify:

```text
eshop.order.id
    29ae3072-1111-2222-3333-444444444444
```

The value must equal the Order ID copied from the Operations Console.

This establishes the bridge between the business entity shown in the Operations Console and the telemetry in Aspire.

## Inventory processing

The Inventory service normally reacts asynchronously to an integration event.

Inside the same distributed trace, locate a span belonging to:

```text
inventory-service
```

Look for a RabbitMQ/message-processing span rather than only an HTTP span.

Typical messaging attributes include values conceptually similar to:

```text
messaging.system
    rabbitmq

messaging.operation.type
    process
```

Then inspect the custom business attributes.

Verify:

```text
eshop.order.id
    29ae3072-1111-2222-3333-444444444444
```

The Order ID must be the same as in the Orders span.

Also inspect:

```text
eshop.correlation_id
```

The correlation value links the messaging operation to the surrounding asynchronous workflow.

The important verification is:

```text
Orders span
eshop.order.id = X

Inventory consumer span
eshop.order.id = X
```

If both contain the same value, the business correlation is working across the asynchronous boundary.

## Payments processing

In the same distributed trace, locate a messaging-processing span belonging to:

```text
payments-service
```

Again, look for the span processing the RabbitMQ message.

Verify:

```text
eshop.order.id
    29ae3072-1111-2222-3333-444444444444
```

The value must still match the original order.

Inspect:

```text
eshop.correlation_id
```

The Payments processing flow should also contain a Payment ID after the Payment entity has been created:

```text
eshop.payment.id
    c31c....
```

The Payment ID may appear on a child processing/application span rather than necessarily on the first consumer span.

Follow the child spans if necessary.

The important relationship is:

```text
Order
  eshop.order.id = X

Inventory processing
  eshop.order.id = X

Payments processing
  eshop.order.id = X
  eshop.payment.id = Y
```

## Cross-check in the Operations Console

After identifying the Payment ID or Order ID in Aspire, return to the Operations Console.

You can use:

```text
Orders
Payments
Inventory
Investigate
```

to inspect the corresponding business state.

For example:

```text
Aspire trace
    ¡
eshop.order.id
    ¡
Operations Console › Investigate › Order
```

or:

```text
Operations Console › Order
    ¡
Open payments
    ¡
Payment state
```

The Operations Console represents business state.

Aspire represents execution and telemetry.

Use both together when investigating distributed failures.

## What to inspect when something fails

For a failed workflow, inspect:

```text
span status
exception information
HTTP status codes
messaging processing spans
span duration
service boundaries
eshop.order.id
eshop.payment.id
eshop.correlation_id
```

Start with the first span reporting an error rather than the last visible failure.

A downstream error may only be a consequence of an earlier failure.

## Interpreting trace structure

A distributed trace is not a list of database entities.

It represents execution.

For example:

```text
POST /orders
-
+¦ orders-service
-
+¦ RabbitMQ publish
-
+¦ inventory-service process
-
+¦ RabbitMQ publish
-
L¦ payments-service process
```

The exact hierarchy can differ depending on where messages are published and consumed.

Do not expect every service to appear as an HTTP call.

Inventory and Payments frequently appear through messaging spans because their work is triggered asynchronously.

## Order ID vs Trace ID vs Correlation ID

These identifiers have different purposes.

### Order ID

```text
eshop.order.id
```

Business identifier.

Use it to find the same order in the Operations Console and across telemetry.

### Payment ID

```text
eshop.payment.id
```

Business identifier for a Payment.

### Trace ID

OpenTelemetry identifier representing one distributed execution trace.

A new HTTP request made later for the same order normally has a different Trace ID.

### Correlation ID

```text
eshop.correlation_id
```

Messaging correlation metadata used to associate related asynchronous processing.

It is not a replacement for the Order ID.

For operational investigation, Order ID is normally the most useful starting identifier.

## Acceptance criteria

A successful observability verification must demonstrate:

```text
1. A checkout produces an Aspire distributed trace.

2. The trace includes the API Gateway and Orders processing.

3. Inventory asynchronous processing is visible.

4. Payments asynchronous processing is visible.

5. Orders and Inventory spans expose the same eshop.order.id.

6. Payments processing exposes the same eshop.order.id.

7. A created Payment exposes eshop.payment.id.

8. Messaging spans expose eshop.correlation_id.

9. The Order ID can be cross-checked in the Operations Console.

10. The Operations Console can open the Aspire Dashboard without querying telemetry itself.
```

## Architecture decision

The Operations Console intentionally does not implement its own trace-query API.

Responsibilities remain separated:

```text
Operations Console
    business state
    business identifiers
    operational workflows

Aspire / OpenTelemetry
    traces
    spans
    service dependencies
    messaging execution
    duration
    errors
```

A dedicated telemetry-query API should only be introduced later if a concrete operational requirement cannot be satisfied by this hand-off model.