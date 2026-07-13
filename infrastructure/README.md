# Infrastructure

This folder contains local infrastructure configuration for the Eshop capstone project.

The project is intended to run locally with Docker Compose.

## Current Infrastructure

The following infrastructure components are currently configured:

* PostgreSQL
* Redis
* RabbitMQ
* RabbitMQ Management UI

## Planned Infrastructure

The following infrastructure components will be added later:

* Keycloak
* Aspire Dashboard

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

The basket is temporary state. After checkout, the durable order must be stored in Orders Service.

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

## Local URLs

| Component              | URL                      |
| ---------------------- | ------------------------ |
| PostgreSQL             | `localhost:5432`         |
| Redis                  | `localhost:6379`         |
| RabbitMQ AMQP          | `localhost:5672`         |
| RabbitMQ Management UI | `http://localhost:15672` |

Default RabbitMQ credentials for local development:

```text
username: eshop
password: eshop_password
```

## Useful Commands

Start infrastructure:

```bash
docker compose up -d postgres redis rabbitmq
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

```text
http://localhost:15672
```

## Scope

This folder does not contain:

* Kubernetes
* Helm
* Terraform
* cloud deployment configuration
* production secrets management

These are intentionally outside the first version of this portfolio project.
