#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." &&
  pwd
)"

OUTPUT_DIRECTORY="${ROOT_DIRECTORY}/infrastructure/dev-data"
REALM_FILE="${ROOT_DIRECTORY}/infrastructure/dev-data/keycloak/eshop-realm.json"
REALM_NAME="eshop"

usage() {
  cat <<'EOF'
Usage: bash scripts/dev/export-dev-data.sh [options]

Exports the current development Keycloak realm and application database data
into repository files that can be restored on another computer.

Options:
  --output-dir PATH  Database dump directory.
                     Default: infrastructure/dev-data
  --realm-file PATH  Keycloak realm output file.
                     Default: infrastructure/dev-data/keycloak/eshop-realm.json
  --help             Show this help.

Stop all locally running backend services and let RabbitMQ finish pending work
before running this script. PostgreSQL is kept online because pg_dump needs it.
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
    --output-dir)
      [[ $# -ge 2 ]] || {
        echo "Missing value for --output-dir." >&2
        exit 2
      }
      OUTPUT_DIRECTORY="$(absolute_path "$2")"
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

command -v docker >/dev/null 2>&1 || {
  echo "Required command is not available: docker" >&2
  exit 1
}

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

mkdir -p "${ROOT_DIRECTORY}/artifacts"

STAGING_DIRECTORY="$(
  mktemp -d \
    "${ROOT_DIRECTORY}/artifacts/dev-data-export.XXXXXX"
)"

cleanup() {
  local exit_code=$?

  trap - EXIT INT TERM

  if [[ -n "${STAGING_DIRECTORY:-}" &&
        "${STAGING_DIRECTORY}" == \
          "${ROOT_DIRECTORY}/artifacts/dev-data-export."* ]]; then
    rm -rf -- "${STAGING_DIRECTORY}"
  fi

  exit "${exit_code}"
}

trap cleanup EXIT INT TERM

cd "${ROOT_DIRECTORY}"

echo "IMPORTANT: backend services must be stopped and message queues drained."
echo "Starting PostgreSQL if necessary..."
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

mkdir -p \
  "${STAGING_DIRECTORY}/keycloak" \
  "${STAGING_DIRECTORY}/databases"

EXPORTED_REALM_FILE="${STAGING_DIRECTORY}/keycloak/${REALM_NAME}-realm.json"

bash \
  "${ROOT_DIRECTORY}/scripts/dev/export-keycloak-dev-data.sh" \
  --realm-file "${EXPORTED_REALM_FILE}"

SERVICES=(
  catalog
  orders
  inventory
  payments
  notifications
)

for service in "${SERVICES[@]}"; do
  database_name="${DATABASE_NAMES[${service}]}"

  if [[ ! "${database_name}" =~ ^[A-Za-z0-9_.-]+$ ]]; then
    echo "Unsafe database name: ${database_name}" >&2
    exit 1
  fi

  container_dump_file="/tmp/eshop-${service}-data-$$.sql"
  staging_dump_file="${STAGING_DIRECTORY}/databases/${database_name}.sql"
  docker_staging_dump_file="$(docker_host_path "${staging_dump_file}")"

  echo "Exporting ${database_name} to ${database_name}.sql..."
  docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    pg_dump \
    --username="${POSTGRES_USERNAME}" \
    --dbname="${database_name}" \
    --data-only \
    --inserts \
    --column-inserts \
    --no-owner \
    --no-privileges \
    '--exclude-table-data=public."__EFMigrationsHistory"' \
    --file="${container_dump_file}"

  docker_cli cp \
    "${POSTGRES_CONTAINER_ID}:${container_dump_file}" \
    "${docker_staging_dump_file}"

  docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    rm -f "${container_dump_file}"

  if [[ ! -s "${staging_dump_file}" ]]; then
    echo "The ${service} database dump is empty or missing." >&2
    exit 1
  fi

  if grep -q \
    '^INSERT INTO public\."__EFMigrationsHistory"' \
    "${staging_dump_file}"; then
    echo \
      "The ${service} dump unexpectedly contains EF migration history." \
      >&2
    exit 1
  fi
done

mkdir -p \
  "$(dirname "${REALM_FILE}")" \
  "${OUTPUT_DIRECTORY}"

cp "${EXPORTED_REALM_FILE}" "${REALM_FILE}"

for service in "${SERVICES[@]}"; do
  database_name="${DATABASE_NAMES[${service}]}"

  cp \
    "${STAGING_DIRECTORY}/databases/${database_name}.sql" \
    "${OUTPUT_DIRECTORY}/${database_name}.sql"
done

echo
echo "Development data export completed."
echo "Realm: ${REALM_FILE}"
echo "Database dumps: ${OUTPUT_DIRECTORY}"
echo
echo "Review the realm for client secrets before committing it."
echo "Suggested review command:"
echo "  git diff -- infrastructure/dev-data/keycloak infrastructure/dev-data/*.sql"
