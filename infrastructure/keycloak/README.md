# Keycloak Local Realm

This folder contains the reproducible local Keycloak realm configuration for the Eshop capstone project.

The `eshop` realm is imported automatically when the Keycloak container starts and the realm does not already exist.

## Local URLs

### Keycloak

```text
http://localhost:18080
```

### Keycloak Admin Console

```text
http://localhost:18080/admin/
```

### Eshop Account Console

```text
http://localhost:18080/realms/eshop/account/
```

### OpenID Connect Discovery Document

```text
http://localhost:18080/realms/eshop/.well-known/openid-configuration
```

## Keycloak Administration

The Keycloak bootstrap administrator belongs to the `master` realm.

Default local credentials:

```text
Username: admin
Password: admin_password
```

These credentials are intended only for local development.

The `master` realm must not be used by the Eshop application.

## Application Realm

Realm name:

```text
eshop
```

The application uses this dedicated realm for authentication and authorization.

## Realm Roles

The realm defines the following application roles:

| Role | Purpose |
|---|---|
| `customer` | Customer-facing basket and order operations |
| `support` | Support and operational read access |
| `admin` | Application administration |

The `admin` role is an Eshop application role.

It does not grant access to the Keycloak Admin Console and does not include any `realm-management` permissions.

## Local Users

| Username | Password | Role |
|---|---|---|
| `alice.customer` | `Alice123!` | `customer` |
| `sam.support` | `Support123!` | `support` |
| `anna.admin` | `Admin123!` | `admin` |

These users and passwords are intended only for local development and local verification.

They must not be reused in production environments.

## Clients

### `eshop-frontend`

Public OpenID Connect client for the React SPA.

Configuration:

- Authorization Code Flow enabled
- PKCE required with `S256`
- implicit flow disabled
- Resource Owner Password Credentials grant disabled
- service account disabled
- no client secret
- redirect URI: `http://localhost:5173/*`
- web origin: `http://localhost:5173`

The client adds `eshop-api` to the access-token audience.

### `eshop-api`

Bearer-only OpenID Connect client representing the API Gateway and backend APIs.

Configuration:

- bearer-only client
- browser login flow disabled
- direct access grants disabled
- service account disabled
- no interactive login

The API Gateway will validate access tokens against this audience in a later implementation step.

## Token Claims

Keycloak provides standard OpenID Connect claims, including:

- `sub`
- `email`
- `preferred_username`

Realm roles remain available in the standard claim:

```text
realm_access.roles
```

The frontend client also maps realm roles to the convenience claim:

```text
roles
```

Access tokens issued for `eshop-frontend` include the API audience:

```text
eshop-api
```

## Automatic Realm Import

Docker Compose starts Keycloak with:

```text
start-dev --import-realm
```

The realm file is mounted inside the container as:

```text
/opt/keycloak/data/import/eshop-realm.json
```

Keycloak imports the file only when the `eshop` realm does not already exist.

The import file is an initial development seed. It is not a migration mechanism for an existing realm.

## Starting Keycloak

Validate the Docker Compose configuration:

```bash
docker compose config --quiet
```

Start PostgreSQL and Keycloak:

```bash
docker compose up -d --force-recreate postgres keycloak
```

Check container state:

```bash
docker compose ps keycloak
```

Check readiness:

```bash
docker inspect \
  --format='{{.State.Health.Status}}' \
  eshop-keycloak
```

Expected result:

```text
healthy
```

Verify the OpenID Connect discovery endpoint:

```bash
curl --fail \
  --silent \
  --show-error \
  http://localhost:18080/realms/eshop/.well-known/openid-configuration \
  > /dev/null
```

## Git Bash Path Conversion

Git Bash automatically converts Linux paths beginning with `/` into Windows paths.

For example:

```text
/opt/keycloak/bin/kcadm.sh
```

may be incorrectly converted to:

```text
C:/Program Files/Git/opt/keycloak/bin/kcadm.sh
```

Disable this conversion for individual Docker commands by using:

```bash
MSYS_NO_PATHCONV=1 docker exec ...
```

Alternatively, disable path conversion for the current Git Bash session:

```bash
export MSYS_NO_PATHCONV=1
```

## Keycloak Administration CLI

The following commands use an explicit configuration file:

```text
/tmp/eshop-kcadm.config
```

This ensures that authentication tokens are reused between separate `docker exec` calls.

### Authenticate

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh config credentials \
  --config /tmp/eshop-kcadm.config \
  --server http://localhost:8080 \
  --realm master \
  --user admin \
  --password admin_password
```

Expected output:

```text
Logging into http://localhost:8080 as user admin of realm master
```

The `--realm master` argument identifies the realm in which the administrator authenticates.

### Verify the Application Realm

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get realms/eshop \
  --config /tmp/eshop-kcadm.config \
  --fields realm,enabled,displayName
```

### Verify Realm Roles

Use the short `-r` option to select the target realm for Admin REST operations:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get roles \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --fields name
```

The output must include:

```text
customer
support
admin
```

Keycloak system roles may also be present.

### Verify Users

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get users \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --fields username,enabled,email
```

The output must include:

```text
alice.customer
sam.support
anna.admin
```

### Verify the Frontend Client

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get clients \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --query clientId=eshop-frontend \
  --fields clientId,publicClient,standardFlowEnabled,directAccessGrantsEnabled
```

Expected values:

```text
clientId: eshop-frontend
publicClient: true
standardFlowEnabled: true
directAccessGrantsEnabled: false
```

### Verify the API Client

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get clients \
  -r eshop \
  --config /tmp/eshop-kcadm.config \
  --query clientId=eshop-api \
  --fields clientId,bearerOnly,standardFlowEnabled,directAccessGrantsEnabled
```

Expected values:

```text
clientId: eshop-api
bearerOnly: true
standardFlowEnabled: false
directAccessGrantsEnabled: false
```

## Authentication Realm vs Target Realm

For administrator authentication, use:

```bash
config credentials --realm master
```

This specifies the realm in which the administrator account exists.

For operations against the `eshop` realm, use:

```bash
get users -r eshop
```

The short `-r` option specifies the target realm of the Admin REST operation.

Do not replace `-r eshop` with `--realm eshop` in the verification commands.

## Removing the CLI Session

Delete the stored `kcadm` configuration:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  rm -f /tmp/eshop-kcadm.config
```

Authenticate again before running further administration commands.

## Reimporting the Realm

Startup import does not overwrite an existing realm.

After changing `eshop-realm.json`, authenticate through `kcadm` and delete only the application realm:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh delete realms/eshop \
  --config /tmp/eshop-kcadm.config
```

Restart Keycloak:

```bash
docker compose restart keycloak
```

Because the `eshop` realm no longer exists, the startup import recreates it from:

```text
infrastructure/keycloak/eshop-realm.json
```

Avoid using:

```bash
docker compose down -v
```

only to refresh the realm.

The PostgreSQL volume is shared with the databases of the other services, so removing volumes would delete the complete local application state.

## Troubleshooting

### `kcadm.sh` Is Converted to a Windows Path

Example error:

```text
exec: "C:/Program Files/Git/opt/keycloak/bin/kcadm.sh":
no such file or directory
```

Use:

```bash
MSYS_NO_PATHCONV=1 docker exec ...
```

### Realm Verification Works but Users, Roles or Clients Return `401`

Ensure that:

1. the same explicit config file is used in every command:

   ```text
   /tmp/eshop-kcadm.config
   ```

2. administrator authentication uses:

   ```bash
   --realm master
   ```

3. operations against the application realm use:

   ```bash
   -r eshop
   ```

Example:

```bash
MSYS_NO_PATHCONV=1 docker exec eshop-keycloak \
  /opt/keycloak/bin/kcadm.sh get users \
  -r eshop \
  --config /tmp/eshop-kcadm.config
```

### Administrator Login Returns `401`

Verify that the same credentials work in the Admin Console:

```text
http://localhost:18080/admin/
```

Bootstrap environment variables create the administrator only during the initial Keycloak database initialization.

Changing these variables later does not automatically change the password of an existing administrator account.

## Security Notes

- Do not store a client secret in React.
- Do not expose Keycloak administrator credentials to frontend code.
- Do not log access tokens, refresh tokens or passwords.
- Do not use local development passwords in production.
- Do not rely on hidden frontend elements as an authorization mechanism.
- Enforce authorization in the API Gateway and relevant downstream services.
- Use HTTPS and production Keycloak mode outside local development.