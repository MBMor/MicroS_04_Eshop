#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." &&
  pwd
)"

REALM_NAME="eshop"
REALM_FILE="${ROOT_DIRECTORY}/infrastructure/dev-data/keycloak/eshop-realm.json"

usage() {
  cat <<'EOF'
Usage: bash scripts/dev/bootstrap-keycloak-dev-data.sh [options]

Starts PostgreSQL and Keycloak and imports the versioned development realm.
The Keycloak database must be empty; this script never deletes an existing
realm or PostgreSQL volume.

Options:
  --realm-file PATH  Realm import file.
                     Default: infrastructure/dev-data/keycloak/eshop-realm.json
  --help             Show this help.
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

for command_name in docker; do
  command -v "${command_name}" >/dev/null 2>&1 || {
    echo "Required command is not available: ${command_name}" >&2
    exit 1
  }
done

PYTHON_COMMAND=""

for candidate in python3 python; do
  if command -v "${candidate}" >/dev/null 2>&1 &&
     "${candidate}" -c 'import sys; print(sys.version)' >/dev/null 2>&1; then
    PYTHON_COMMAND="${candidate}"
    break
  fi
done

if [[ -z "${PYTHON_COMMAND}" ]]; then
  echo "Python 3 is required to validate the Keycloak import." >&2
  exit 1
fi

WINDOWS_POSIX_SHELL=false

case "${OSTYPE:-}" in
  msys* | cygwin*)
    WINDOWS_POSIX_SHELL=true
    ;;
esac

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

if [[ ! -s "${REALM_FILE}" ]]; then
  echo "Keycloak realm file is missing: ${REALM_FILE}" >&2
  exit 1
fi

"${PYTHON_COMMAND}" - "${REALM_FILE}" "${REALM_NAME}" <<'PY'
import json
import sys
from pathlib import Path

realm_path = Path(sys.argv[1])
expected_realm = sys.argv[2]

with realm_path.open(encoding="utf-8") as realm_file:
    realm = json.load(realm_file)

if realm.get("realm") != expected_realm:
    raise SystemExit(
        f"Expected realm {expected_realm!r}, got {realm.get('realm')!r}."
    )

users = realm.get("users") or []
if not users:
    raise SystemExit("The realm import does not contain any users.")

missing_ids = [
    user.get("username", "<unknown>")
    for user in users
    if not user.get("id")
]
if missing_ids:
    raise SystemExit(
        "Stable Keycloak user IDs are required by the restored customer data: "
        + ", ".join(missing_ids)
    )

print(f"Validated Keycloak realm with {len(users)} user(s).")
PY

cd "${ROOT_DIRECTORY}"

echo "Starting PostgreSQL..."
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
KEYCLOAK_DATABASE="$(
  container_environment_value \
    "${POSTGRES_CONTAINER_ID}" \
    KEYCLOAK_DB \
    keycloak_db
)"

KEYCLOAK_SCHEMA_EXISTS="$(
  docker_cli exec \
    "${POSTGRES_CONTAINER_ID}" \
    psql \
    --tuples-only \
    --no-align \
    --username="${POSTGRES_USERNAME}" \
    --dbname="${KEYCLOAK_DATABASE}" \
    --command="SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public');" |
    tr -d '\r'
)"

if [[ "${KEYCLOAK_SCHEMA_EXISTS}" == "t" ]]; then
  echo \
    "The Keycloak database already has a schema. " \
    "Use a fresh PostgreSQL volume; no data was deleted." >&2
  exit 1
fi

echo "Starting Keycloak and importing realm ${REALM_NAME}..."
compose up \
  --detach \
  --wait \
  --force-recreate \
  keycloak

echo "Keycloak realm bootstrap completed: ${REALM_FILE}"
