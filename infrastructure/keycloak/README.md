# Keycloak Local Realm Plan

This folder contains the local Keycloak plan for the Eshop capstone project.

The actual realm import file will be added later.

## Current Step

Keycloak is currently added as a Docker Compose service.

It runs in local development mode and uses PostgreSQL as persistent storage.

## Local URL

Keycloak Admin Console:

`http://localhost:18080`

Default local admin credentials:

* username: `admin`
* password: `admin_password`

These credentials are for local development only.

## Planned Realm

Realm name:

`eshop`

The `master` realm must be used only for Keycloak administration.

The application should use the dedicated `eshop` realm.

## Planned Realm Roles

The first version will use simple realm roles:

* `customer`
* `support`
* `admin`

## Planned Local Users

| User             | Role       |
| ---------------- | ---------- |
| `alice.customer` | `customer` |
| `sam.support`    | `support`  |
| `anna.admin`     | `admin`    |

## Planned Clients

### `eshop-frontend`

Purpose:

* React SPA login
* Authorization Code Flow with PKCE
* no client secret in frontend

Planned local redirect URI:

`http://localhost:5173/*`

Planned local web origin:

`http://localhost:5173`

### `eshop-api`

Purpose:

* API Gateway and backend audience
* JWT validation target
* role and claim mapping

The exact client setup will be added later when JWT authentication is implemented.

## Planned Token Claims

The backend will need access to:

* `sub`
* `email`
* `preferred_username`
* `roles`

The exact Keycloak role mapper will be configured later.

## Important Security Notes

Do not store frontend secrets in React.

Do not expose Keycloak admin credentials in frontend code.

Do not log JWT tokens.

Do not log passwords.

UI hiding is not security.

Authorization must be enforced by API Gateway and downstream services.
