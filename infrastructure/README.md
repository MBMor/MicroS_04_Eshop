# Infrastructure

This folder contains local infrastructure configuration for the Eshop capstone project.

The project is intended to run locally with Docker Compose.

## Current Infrastructure

The following infrastructure components are currently configured:

* PostgreSQL
* Redis
* RabbitMQ
* RabbitMQ Management UI
* Keycloak
* Aspire Dashboard

## Planned Infrastructure

No additional infrastructure components are planned for the current infrastructure phase.

Application services will be added in later steps.

## Docker Compose Networks

The root `docker-compose.yml` defines two networks.

### `eshop-public`

Used for services that expose local development ports, such as:

* React frontend
* API Gateway
* Keycloak
* RabbitMQ Management UI
* Aspire Dashboard

### `eshop-internal`

Used for backend-to-backend and backend-to-infrastructure communication.

This network is marked as internal.

Typical services on this network:

* backend services
* PostgreSQL
* Redis
* RabbitMQ
* Aspire Dashboard OTLP endpoints

## PostgreSQL

PostgreSQL is configured as a single local container with multiple logical databases.

Databases:

* `catalog_db`
* `orders_db`
* `inventory_db`
* `payments_db`
* `notifications_db`
* `keycloak_db`

This is a practical local-development compromise.

Each microservice still owns its own logical database.

Planned ownership:

| Database           | Owner                 |
| ------------------ | --------------------- |
| `catalog_db`       | Catalog Service       |
| `orders_db`        | Orders Service        |
| `inventory_db`     | Inventory Service     |
| `payments_db`      | Payments Service      |
| `notifications_db` | Notifications Service |
| `keycloak_db`      | Keycloak              |

Redis is not used for durable business data.

## Redis

Redis is used for Basket Service state.

Planned usage:

* user basket
* temporary basket item snapshots
* basket expiration

Redis must not be used as the source of truth for:

* orders
* payments
* inventory
* catalog data

The basket is temporary state.

After checkout, the durable order must be stored in Orders Service.

## RabbitMQ

RabbitMQ is used for asynchronous integration between services.

The management UI is exposed for local debugging.

Planned exchange:

* `eshop.events`

Planned routing keys:

* `order.created`
* `stock.reserved`
* `stock.reservation.failed`
* `payment.requested`
* `payment.authorized`
* `payment.failed`
* `order.cancelled`

Queues and dead-letter queues will be added later.

## Keycloak

Keycloak is used as the Identity Provider.

Planned responsibilities:

* users
* roles
* login
* JWT issuing
* identity claims

The application will use a dedicated realm:

* `eshop`

Planned roles:

* `customer`
* `support`
* `admin`

Keycloak uses PostgreSQL database:

* `keycloak_db`

Business services must not implement custom password storage.

## Aspire Dashboard

Aspire Dashboard is used as a local observability dashboard.

It will later display:

* structured logs
* distributed traces
* metrics
* service telemetry

Current local dashboard URL:

* `http://localhost:18888`

Host OTLP endpoints:

| Protocol  | Endpoint                |
| --------- | ----------------------- |
| OTLP/gRPC | `http://localhost:4317` |
| OTLP/HTTP | `http://localhost:4318` |

Docker Compose service OTLP endpoints:

| Protocol  | Endpoint                        |
| --------- | ------------------------------- |
| OTLP/gRPC | `http://aspire-dashboard:18889` |
| OTLP/HTTP | `http://aspire-dashboard:18890` |

For local development, anonymous access is enabled.

Do not expose the dashboard publicly without authentication.

## Local URLs

| Component              | URL                      |
| ---------------------- | ------------------------ |
| PostgreSQL             | `localhost:5432`         |
| Redis                  | `localhost:6379`         |
| RabbitMQ AMQP          | `localhost:5672`         |
| RabbitMQ Management UI | `http://localhost:15672` |
| Keycloak Admin Console | `http://localhost:18080` |
| Aspire Dashboard       | `http://localhost:18888` |

Default RabbitMQ credentials for local development:

* username: `eshop`
* password: `eshop_password`

Default Keycloak admin credentials for local development:

* username: `admin`
* password: `admin_password`

## Useful Commands

Start infrastructure:

```bash
docker compose up -d postgres redis rabbitmq keycloak aspire-dashboard
```

Show containers:

```bash
docker compose ps
```

Show PostgreSQL logs:

```bash
docker compose logs -f postgres
```

Show Redis logs:

```bash
docker compose logs -f redis
```

Show RabbitMQ logs:

```bash
docker compose logs -f rabbitmq
```

Show Keycloak logs:

```bash
docker compose logs -f keycloak
```

Show Aspire Dashboard logs:

```bash
docker compose logs -f aspire-dashboard
```

Stop containers:

```bash
docker compose down
```

Stop containers and remove volumes:

```bash
docker compose down -v
```

## Verification

Verify PostgreSQL:

```bash
docker exec -it eshop-postgres psql -U eshop -d postgres -c "\l"
```

Expected logical databases:

* `catalog_db`
* `orders_db`
* `inventory_db`
* `payments_db`
* `notifications_db`
* `keycloak_db`

Verify Redis:

```bash
docker exec -it eshop-redis redis-cli ping
```

Expected result:

```text
PONG
```

Verify RabbitMQ:

```bash
docker exec -it eshop-rabbitmq rabbitmq-diagnostics check_running
```

Expected result:

```text
RabbitMQ is running
```

Open RabbitMQ Management UI:

* `http://localhost:15672`

Verify Keycloak:

Open Keycloak Admin Console:

* `http://localhost:18080`

Login with local development credentials:

* username: `admin`
* password: `admin_password`

Verify Aspire Dashboard:

Open Aspire Dashboard:

* `http://localhost:18888`

Expected result:

* Aspire Dashboard opens without requiring a login token.
* No application traces or metrics are visible yet because backend services are not exporting telemetry yet.

## Existing Volume Note

PostgreSQL initialization scripts run only when the PostgreSQL data volume is created for the first time.

If `keycloak_db` is missing because the PostgreSQL volume already existed before Keycloak was added, use one of the following options.

### Option A: Remove local volumes

Use this if you do not need to keep local database data:

```bash
docker compose down -v
docker compose up -d postgres redis rabbitmq keycloak aspire-dashboard
```

### Option B: Create the Keycloak database manually

Use this if you want to keep existing local volumes:

```bash
docker exec -it eshop-postgres createdb -U eshop keycloak_db
```

If the database already exists, the command may fail with a `database already exists` message.

In that case, no action is needed.

## Scope

This folder does not contain:

* Kubernetes
* Helm
* Terraform
* cloud deployment configuration
* production secrets management

These are intentionally outside the first version of this portfolio project.
