# Infrastructure

This folder contains local infrastructure configuration for the Eshop capstone project.

The project is intended to run locally with Docker Compose.

## Planned infrastructure

The following infrastructure components will be added step by step:

- PostgreSQL
- Redis
- RabbitMQ
- RabbitMQ Management UI
- Keycloak
- Aspire Dashboard

## Docker Compose networks

The root `docker-compose.yml` defines two networks.

### `eshop-public`

Used for services that expose local development ports, such as:

- React frontend
- API Gateway
- Keycloak
- RabbitMQ Management UI
- Aspire Dashboard

### `eshop-internal`

Used for backend-to-backend and backend-to-infrastructure communication.

This network is marked as internal.

Typical services on this network:

- backend services
- PostgreSQL
- Redis
- RabbitMQ

## Scope

This folder does not contain Kubernetes, Helm, Terraform, or cloud deployment configuration.

Those are intentionally outside the first version of this portfolio project.
