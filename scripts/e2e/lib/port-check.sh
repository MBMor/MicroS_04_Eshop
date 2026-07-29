#!/usr/bin/env bash

is_e2e_windows_platform() {
  local platform="${E2E_PORT_CHECK_PLATFORM:-${OSTYPE:-}}"

  [[ "${platform}" == msys* ||
     "${platform}" == cygwin* ]]
}

is_port_in_use() {
  local port="$1"

  if is_e2e_windows_platform; then
    netstat -ano 2>/dev/null |
      tr -d '\r' |
      grep -E \
        "[\.:]${port}[[:space:]].*LISTENING" \
        >/dev/null

    return
  fi

  if command -v ss >/dev/null 2>&1; then
    ss \
      -ltn \
      "sport = :${port}" \
      2>/dev/null |
      grep -E \
        "[\.:]${port}[[:space:]]" \
        >/dev/null

    return
  fi

  netstat -ltn 2>/dev/null |
    tr -d '\r' |
    grep -E \
      "[\.:]${port}[[:space:]].*LISTEN" \
      >/dev/null
}

print_backend_port_owners() {
  if is_e2e_windows_platform; then
    netstat -ano 2>/dev/null |
      tr -d '\r' |
      grep -E \
        ':(5080|5081|5082|5083|5084|5085|5086)[[:space:]].*LISTENING' ||
      true

    return
  fi

  if command -v ss >/dev/null 2>&1; then
    ss \
      -ltnp \
      2>/dev/null |
      grep -E \
        ':(5080|5081|5082|5083|5084|5085|5086)[[:space:]]' ||
      true

    return
  fi

  netstat -ltnp 2>/dev/null |
    grep -E \
      ':(5080|5081|5082|5083|5084|5085|5086)[[:space:]]' ||
    true
}

assert_backend_ports_are_free() {
  local occupied_ports=()

  for port in "${BACKEND_PORTS[@]}"; do
    if is_port_in_use "${port}"; then
      occupied_ports+=("${port}")
    fi
  done

  if ((${#occupied_ports[@]} == 0)); then
    echo "Backend ports 5080-5086 are free."
    return 0
  fi

  echo
  echo "The following E2E backend ports are already in use:"

  printf '  - %s\n' "${occupied_ports[@]}"

  echo
  echo "Listening processes:"

  print_backend_port_owners

  echo
  echo "Stop Visual Studio, dotnet run processes, or another E2E stack."

  return 1
}
