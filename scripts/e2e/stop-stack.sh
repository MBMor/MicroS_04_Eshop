#!/usr/bin/env bash

set -u

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." &&
  pwd
)"

PID_FILE="${ROOT_DIRECTORY}/artifacts/e2e/backend-processes.pid"

COMPOSE_COMMAND=(
  docker compose
  --file "${ROOT_DIRECTORY}/docker-compose.yml"
  --file "${ROOT_DIRECTORY}/docker-compose.e2e.yml"
)

is_process_running() {
  local process_id="$1"

  kill -0 "${process_id}" 2>/dev/null
}

stop_process() {
  local process_id="$1"

  if [[ ! "${process_id}" =~ ^[0-9]+$ ]]; then
    echo "Skipping invalid backend PID: ${process_id}"
    return
  fi

  if ! is_process_running "${process_id}"; then
    return
  fi

  echo "Stopping backend process ${process_id}..."

  if [[ "${OSTYPE:-}" == msys* ||
        "${OSTYPE:-}" == cygwin* ]]
  then
    MSYS_NO_PATHCONV=1 taskkill \
      /PID "${process_id}" \
      /T \
      /F >/dev/null 2>&1 ||
      true
  else
    kill "${process_id}" 2>/dev/null ||
      true
  fi

  for _ in {1..20}; do
    if ! is_process_running "${process_id}"; then
      return
    fi

    sleep 0.25
  done

  echo "Force stopping backend process ${process_id}..."

  kill -9 "${process_id}" 2>/dev/null ||
    true

  if is_process_running "${process_id}"; then
    echo "Warning: backend process ${process_id} is still running."
  fi
}

if [[ -f "${PID_FILE}" ]]; then
  mapfile -t PROCESS_IDS <"${PID_FILE}"

  for ((
    index = ${#PROCESS_IDS[@]} - 1;
    index >= 0;
    index--
  )); do
    process_id="${PROCESS_IDS[index]}"

    if [[ -n "${process_id}" ]]; then
      stop_process "${process_id}"
    fi
  done

  rm -f "${PID_FILE}"
fi

echo "Stopping E2E infrastructure..."

"${COMPOSE_COMMAND[@]}" down \
  --volumes \
  --remove-orphans ||
  true

echo "E2E stack stopped."