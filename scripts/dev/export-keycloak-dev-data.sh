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
Usage: bash scripts/dev/export-keycloak-dev-data.sh [options]

Exports the current development Keycloak realm, including users and their
stable IDs, into the repository development-data directory.

Options:
  --realm-file PATH  Output realm file.
                     Default: infrastructure/dev-data/keycloak/eshop-realm.json
  --help             Show this help.

Keycloak is stopped during the export for a consistent realm snapshot. It is
started again if it was running before this script began.
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

command -v docker >/dev/null 2>&1 || {
  echo "Required command is not available: docker" >&2
  exit 1
}

PYTHON_COMMAND=""

for candidate in python3 python; do
  if command -v "${candidate}" >/dev/null 2>&1 &&
     "${candidate}" -c 'import sys; print(sys.version)' >/dev/null 2>&1; then
    PYTHON_COMMAND="${candidate}"
    break
  fi
done

if [[ -z "${PYTHON_COMMAND}" ]]; then
  echo "Python 3 is required to validate the Keycloak export." >&2
  exit 1
fi

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

mkdir -p "${ROOT_DIRECTORY}/artifacts"
STAGING_DIRECTORY="$(
  mktemp -d "${ROOT_DIRECTORY}/artifacts/keycloak-export.XXXXXX"
)"
KEYCLOAK_NEEDS_RESTART=false

cleanup() {
  local exit_code=$?

  trap - EXIT INT TERM

  if [[ "${KEYCLOAK_NEEDS_RESTART}" == true ]]; then
    echo "Restarting Keycloak after interrupted export..." >&2
    compose up \
      --detach \
      --wait \
      --force-recreate \
      keycloak >/dev/null || true
  fi

  if [[ -n "${STAGING_DIRECTORY:-}" &&
        "${STAGING_DIRECTORY}" == \
          "${ROOT_DIRECTORY}/artifacts/keycloak-export."* ]]; then
    rm -rf -- "${STAGING_DIRECTORY}"
  fi

  exit "${exit_code}"
}

trap cleanup EXIT INT TERM

cd "${ROOT_DIRECTORY}"

if [[ -n "$(compose ps --quiet --status running keycloak)" ]]; then
  KEYCLOAK_NEEDS_RESTART=true
  echo "Stopping Keycloak for a consistent full realm export..."
  compose stop keycloak
fi

mkdir -p "${STAGING_DIRECTORY}/export"
DOCKER_EXPORT_DIRECTORY="$(
  docker_host_path "${STAGING_DIRECTORY}/export"
)"

echo "Exporting Keycloak realm ${REALM_NAME}..."
compose run \
  --rm \
  --no-deps \
  --volume \
  "${DOCKER_EXPORT_DIRECTORY}:/opt/keycloak/data/export" \
  keycloak \
  export \
  --optimized \
  --dir /opt/keycloak/data/export \
  --realm "${REALM_NAME}" \
  --users realm_file

EXPORTED_REALM_FILE="${STAGING_DIRECTORY}/export/${REALM_NAME}-realm.json"

if [[ ! -s "${EXPORTED_REALM_FILE}" ]]; then
  echo "Keycloak did not create ${REALM_NAME}-realm.json." >&2
  exit 1
fi

"${PYTHON_COMMAND}" - "${EXPORTED_REALM_FILE}" "${REALM_NAME}" <<'PY'
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
    raise SystemExit("The realm export does not contain any users.")

missing_ids = [
    user.get("username", "<unknown>")
    for user in users
    if not user.get("id")
]
if missing_ids:
    raise SystemExit(
        "The following users do not have stable Keycloak IDs: "
        + ", ".join(missing_ids)
    )

clients_with_secrets = [
    client.get("clientId", "<unknown>")
    for client in realm.get("clients") or []
    if client.get("secret")
]
if clients_with_secrets:
    print(
        "WARNING: exported client secrets require manual review: "
        + ", ".join(clients_with_secrets),
        file=sys.stderr,
    )

print(f"Validated Keycloak realm with {len(users)} user(s).")
PY

mkdir -p "$(dirname "${REALM_FILE}")"
cp "${EXPORTED_REALM_FILE}" "${REALM_FILE}"

if [[ "${KEYCLOAK_NEEDS_RESTART}" == true ]]; then
  echo "Starting Keycloak..."
  compose up \
    --detach \
    --wait \
    --force-recreate \
    keycloak >/dev/null
  KEYCLOAK_NEEDS_RESTART=false
fi

echo "Keycloak realm export completed: ${REALM_FILE}"
