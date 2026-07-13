# Aspire Dashboard

This folder documents the local Aspire Dashboard setup for the Eshop capstone project.

Aspire Dashboard is used as a local observability dashboard for development and diagnostics.

## Current Step

Aspire Dashboard is currently added as a standalone Docker Compose service.

Application telemetry is not exported yet because backend services have not been created.

OpenTelemetry export will be added later.

## Local URL

Aspire Dashboard UI:

* `http://localhost:18888`

## Local OTLP Endpoints

From the host machine:

| Protocol  | Endpoint                |
| --------- | ----------------------- |
| OTLP/gRPC | `http://localhost:4317` |
| OTLP/HTTP | `http://localhost:4318` |

From Docker Compose services:

| Protocol  | Endpoint                        |
| --------- | ------------------------------- |
| OTLP/gRPC | `http://aspire-dashboard:18889` |
| OTLP/HTTP | `http://aspire-dashboard:18890` |

## Docker Compose Service

Service name:

* `aspire-dashboard`

Container name:

* `eshop-aspire-dashboard`

Image:

* `mcr.microsoft.com/dotnet/aspire-dashboard:latest`

## Local Authentication

For local development, anonymous access is enabled:

* `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`

This avoids copying a browser login token from container logs during local development.

Do not use this setting for a publicly exposed dashboard.

## Planned Usage

The dashboard will later receive:

* structured logs
* distributed traces
* metrics
* service names
* HTTP telemetry
* database telemetry
* Redis telemetry where practical
* RabbitMQ publish and consume telemetry
* outbox dispatcher telemetry
* custom business spans

## Planned Backend Environment Variables

Future backend services will use OpenTelemetry exporter configuration similar to:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
OTEL_SERVICE_NAME=<service-name>
```

The exact configuration will be added in the OpenTelemetry steps.

## Important Security Notes

Aspire Dashboard may display sensitive runtime data.

Do not expose the dashboard publicly without authentication.

Do not log secrets.

Do not log JWT tokens.

Do not log passwords.

Do not log full request bodies by default.

## Scope

This setup does not add a full Aspire AppHost.

The dashboard is used standalone.

A full AppHost can be considered later only if it provides clear value.
