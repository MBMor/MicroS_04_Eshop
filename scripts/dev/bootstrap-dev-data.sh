#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." &&
  pwd
)"

DATA_DIRECTORY="${ROOT_DIRECTORY}/infrastructure/dev-data"
REALM_FILE="${ROOT_DIRECTORY}/infrastructure/dev-data/keycloak/eshop-realm.json"
REALM_NAME="eshop"

usage() {
  cat <<'EOF'
Usage: bash scripts/dev/bootstrap-dev-data.sh [options]

Bootstraps a fresh local environment from the versioned Keycloak realm and
application database data dumps. EF Core migrations create the schemas before
the data is restored.

Options:
  --data-dir PATH   Database dump directory.
                    Default: infrastructure/dev-data
  --realm-file PATH Keycloak realm import file.
                    Default: infrastructure/dev-data/keycloak/eshop-realm.json
  --help            Show this help.

Safety:
  - The Keycloak database must not already contain a realm schema.
  - Application tables must be empty after migrations.
  - Each SQL restore runs in a transaction and stops on the first error.

Use this script for a new PostgreSQL volume. It never deletes existing data.
EOF
}

absolute_path() {
  local path="$1"

  path="${path//\\//}"

  case "${path}" in
    /* | [A-Za-z]:/*)
      printf '%s\n' "${path}"
      ;;
    *)
      printf '%s\n' "${ROOT_DIRECTORY}/${path}"
      ;;
  esac
}

while (($# > 0)); do
  case "$1" in
    --data-dir)
      [[ $# -ge 2 ]] || {
        echo "Missing value for --data-dir." >&2
        exit 2
      }
      DATA_DIRECTORY="$(absolute_path "$2")"
      shift 2
      ;;
    --realm-file)
      [[ $# -ge 2 ]] || {
        echo "Missing value for --realm-file." >&2
        exit 2
      }
      REALM_FILE="$(absolute_path "$2")"
      shift 2
      ;;
    --help | -h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

for command_name in docker dotnet; do
  command -v "${command_name}" >/dev/null 2>&1 || {
    echo "Required command is not available: ${command_name}" >&2
    exit 1
  }
done

WINDOWS_POSIX_SHELL=false

case "${OSTYPE:-}" in
  msys* | cygwin*)
    WINDOWS_POSIX_SHELL=true
    ;;
esac

docker_host_path() {
  local path="$1"

  if [[ "${WINDOWS_POSIX_SHELL}" == true ]]; then
    cygpath --mixed "${path}"
  else
    printf '%s\n' "${path}"
  fi
}

docker_cli() {
  if [[ "${WINDOWS_POSIX_SHELL}" == true ]]; then
    MSYS_NO_PATHCONV=1 docker "$@"
  else
    docker "$@"
  fi
}

compose() {
  docker_cli compose "$@"
}

container_environment_value() {
  local container_id="$1"
  local variable_name="$2"
  local default_value="$3"
  local value

  value="$({
    docker_cli inspect \
      --format '{{range .Config.Env}}{{println .}}{{end}}' \
      "${container_id}" |
      sed -n "s/^${variable_name}=//p" |
      head -n 1
  } || true)"

  if [[ -n "${value}" ]]; then
    printf '%s\n' "${value}"
  else
    printf '%s\n' "${default_value}"
  fi
}

SERVICES=(
  catalog
  orders
  inventory
  payments
  notifications
)

cd "${ROOT_DIRECTORY}"

echo "Starting a fresh PostgreSQL environment..."
compose up --detach --wait postgres

POSTGRES_CONTAINER_ID="$(compose ps --quiet postgres)"

if [[ -z "${POSTGRES_CONTAINER_ID}" ]]; then
  echo "The PostgreSQL container is not available." >&2
  exit 1
fi

POSTGRES_USERNAME="$(
  container_environment_value \
    "${POSTGRES_CONTAINER_ID}" \
    POSTGRES_USER \
    eshop
)"
POSTGRES_PASSWORD="$(
  container_environment_value \
    "${POSTGRES_CONTAINER_ID}" \
    POSTGRES_PASSWORD \
    eshop_password
)"
declare -A DATABASE_NAMES=(
  [catalog]="$(
    container_environment_value \
      "${POSTGRES_CONTAINER_ID}" \
      CATALOG_DB \
      catalog_db
  )"
  [orders]="$(
    container_environment_value \
      "${POSTGRES_CONTAINER_ID}" \
      ORDERS_DB \
      orders_db
  )"
  [inventory]="$(
    container_environment_value \
      "${POSTGRES_CONTAINER_ID}" \
      INVENTORY_DB \
      inventory_db
  )"
  [payments]="$(
    container_environment_value \
      "${POSTGRES_CONTAINER_ID}" \
      PAYMENTS_DB \
      payments_db
  )"
  [notifications]="$(
    container_environment_value \
      "${POSTGRES_CONTAINER_ID}" \
      NOTIFICATIONS_DB \
      notifications_db
  )"
)

declare -A PROJECTS=(
  [catalog]="src/backend/services/CatalogService/CatalogService.csproj"
  [orders]="src/backend/services/OrdersService/OrdersService.csproj"
  [inventory]="src/backend/services/InventoryService/InventoryService.csproj"
  [payments]="src/backend/services/PaymentsService/PaymentsService.csproj"
  [notifications]="src/backend/services/NotificationsService/NotificationsService.csproj"
)

for service in "${SERVICES[@]}"; do
  database_name="${DATABASE_NAMES[${service}]}"

  if [[ ! "${database_name}" =~ ^[A-Za-z0-9_.-]+$ ]]; then
    echo "Unsafe database name: ${database_name}" >&2
    exit 1
  fi

  dump_file="${DATA_DIRECTORY}/${database_name}.sql"

  if [[ ! -s "${dump_file}" ]]; then
    echo "Database dump is missing or empty: ${dump_file}" >&2
    exit 1
  fi

  if grep -q \
    '^INSERT INTO public\."__EFMigrationsHistory"' \
    "${dump_file}"; then
    echo \
      "Database dump contains EF migration history: ${dump_file}" \
      >&2
    echo \
      "Regenerate it with scripts/dev/export-dev-data.sh before bootstrap." \
      >&2
    exit 1
  fi
done

POSTGRES_ENDPOINT="$(compose port postgres 5432)"
POSTGRES_PORT="${POSTGRES_ENDPOINT##*:}"

if [[ -z "${POSTGRES_PORT}" ||
      ! "${POSTGRES_PORT}" =~ ^[0-9]+$ ]]; then
  echo "Could not determine the PostgreSQL host port." >&2
  exit 1
fi

echo "Restoring local .NET tools..."
dotnet tool restore

for service in "${SERVICES[@]}"; do
  database_name="${DATABASE_NAMES[${service}]}"
  project="${PROJECTS[${service}]}"
  connection_string="Host=127.0.0.1;Port=${POSTGRES_PORT};Database=${database_name};Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"

  echo "Applying ${service} EF Core migrations..."
  dotnet ef database update \
    --project "${project}" \
    --startup-project "${project}" \
    --configuration Release \
    --connection "${connection_string}"
done

EMPTY_DATABASE_CHECK='DO $check$
DECLARE
  table_record record;
  contains_rows boolean;
BEGIN
  FOR table_record IN
    SELECT schemaname, tablename
    FROM pg_tables
    WHERE schemaname = '\''public'\''
      AND tablename <> '\''__EFMigrationsHistory'\''
  LOOP
    EXECUTE format(
      '\''SELECT EXISTS (SELECT 1 FROM %I.%I LIMIT 1)'\'',
      table_record.schemaname,
      table_record.tablename
    ) INTO contains_rows;

    IF contains_rows THEN
      RAISE EXCEPTION
        '\''Table %.% already contains data.'\'',
        table_record.schemaname,
        table_record.tablename;
    END IF;
  END LOOP;
END
$check$;'

for service in "${SERVICES[@]}"; do
  database_name="${DATABASE_NAMES[${service}]}"
  dump_file="${DATA_DIRECTORY}/${database_name}.sql"
  docker_dump_file="$(docker_host_path "${dump_file}")"
  container_dump_file="/tmp/eshop-${service}-bootstrap-$$.sql"

  echo "Checking that ${database_name} contains no application data..."
  docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    psql \
    --set ON_ERROR_STOP=1 \
    --username="${POSTGRES_USERNAME}" \
    --dbname="${database_name}" \
    --command="${EMPTY_DATABASE_CHECK}"

  echo "Restoring ${database_name}.sql into ${database_name}..."
  docker_cli cp \
    "${docker_dump_file}" \
    "${POSTGRES_CONTAINER_ID}:${container_dump_file}"

  if ! docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    psql \
    --set ON_ERROR_STOP=1 \
    --single-transaction \
    --username="${POSTGRES_USERNAME}" \
    --dbname="${database_name}" \
    --file="${container_dump_file}"; then
    docker_cli exec \
      "${POSTGRES_CONTAINER_ID}" \
      rm -f "${container_dump_file}" || true

    echo \
      "Restore of ${database_name}.sql failed and its transaction was rolled back." \
      >&2
    exit 1
  fi

  docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    rm -f "${container_dump_file}"
done

echo "Starting Keycloak..."
bash \
  "${ROOT_DIRECTORY}/scripts/dev/bootstrap-keycloak-dev-data.sh" \
  --realm-file "${REALM_FILE}"

echo "Starting Redis, RabbitMQ, and Aspire Dashboard..."
compose up \
  --detach \
  --wait \
  redis \
  rabbitmq \
  aspire-dashboard

echo
echo "Development data bootstrap completed."
echo "Keycloak imported realm ${REALM_NAME} from ${REALM_FILE}."
echo "Application schemas were migrated and data was restored from:"
echo "  ${DATA_DIRECTORY}"
echo
echo "Backend services can now be started from Visual Studio or with dotnet run."
