#!/usr/bin/env bash

set -Eeuo pipefail

ROOT_DIRECTORY="$(
  cd "${BASH_SOURCE[0]%/*}/../../.." &&
  pwd
)"

BACKEND_PORTS=(5080 5081 5082 5083 5084 5085 5086)

source "${ROOT_DIRECTORY}/scripts/e2e/lib/port-check.sh"

TEMPORARY_DIRECTORY="$(mktemp -d)"
trap 'rm -rf "${TEMPORARY_DIRECTORY}"' EXIT

FAKE_BIN="${TEMPORARY_DIRECTORY}/bin"
SS_CALL_LOG="${TEMPORARY_DIRECTORY}/ss-calls.log"
NETSTAT_CALL_LOG="${TEMPORARY_DIRECTORY}/netstat-calls.log"

mkdir -p "${FAKE_BIN}"

cat >"${FAKE_BIN}/ss" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
printf '%s\n' "$*" >>"${SS_CALL_LOG}"
if [[ "$*" == "-ltn sport = :5080" ]]; then
  echo "LISTEN 0 512 127.0.0.1:5080 0.0.0.0:*"
elif [[ "$*" == "-ltnp" ]]; then
  echo 'LISTEN 0 512 127.0.0.1:5080 0.0.0.0:* users:(("dotnet",pid=42,fd=1))'
fi
EOF

cat >"${FAKE_BIN}/netstat" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
printf '%s\n' "$*" >>"${NETSTAT_CALL_LOG}"
if [[ "$*" == "-ano" ]]; then
  echo "TCP 0.0.0.0:5080 0.0.0.0:0 LISTENING 42"
fi
EOF

chmod +x "${FAKE_BIN}/ss" "${FAKE_BIN}/netstat"

export PATH="${FAKE_BIN}:${PATH}"
export SS_CALL_LOG
export NETSTAT_CALL_LOG

E2E_PORT_CHECK_PLATFORM="linux-gnu"
export E2E_PORT_CHECK_PLATFORM

if ! is_port_in_use 5080; then
  echo "Expected the mocked Linux port 5080 to be occupied."
  exit 1
fi

if is_port_in_use 5081; then
  echo "Expected the mocked Linux port 5081 to be free."
  exit 1
fi

grep --fixed-strings --line-regexp --quiet \
  -- \
  "-ltn sport = :5080" \
  "${SS_CALL_LOG}"

if grep --fixed-strings --quiet -- "--headers=never" "${SS_CALL_LOG}"; then
  echo "The unsupported ss --headers=never option was used."
  exit 1
fi

owners="$(print_backend_port_owners)"
grep --fixed-strings --quiet "127.0.0.1:5080" <<<"${owners}"
grep --fixed-strings --line-regexp --quiet -- "-ltnp" "${SS_CALL_LOG}"

E2E_PORT_CHECK_PLATFORM="msys"
export E2E_PORT_CHECK_PLATFORM

if ! is_port_in_use 5080; then
  echo "Expected the mocked Windows port 5080 to be occupied."
  exit 1
fi

if is_port_in_use 5081; then
  echo "Expected the mocked Windows port 5081 to be free."
  exit 1
fi

grep --fixed-strings --line-regexp --quiet -- "-ano" "${NETSTAT_CALL_LOG}"

echo "E2E port detection contract passed."
