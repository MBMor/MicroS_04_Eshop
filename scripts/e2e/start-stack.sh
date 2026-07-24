#!/usr/bin/env bash

set -Eeuo pipefail

if [[ "${OSTYPE:-}" == msys* ||
      "${OSTYPE:-}" == cygwin* ]]
then
  if [[ -n "${MSYS2_ENV_CONV_EXCL:-}" ]]; then
    export MSYS2_ENV_CONV_EXCL="${MSYS2_ENV_CONV_EXCL};RabbitMq__VirtualHost"
  else
    export MSYS2_ENV_CONV_EXCL="RabbitMq__VirtualHost"
  fi
fi

ROOT_DIRECTORY="$(
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." &&
  pwd
)"

ARTIFACTS_DIRECTORY="${ROOT_DIRECTORY}/artifacts/e2e"
LOGS_DIRECTORY="${ARTIFACTS_DIRECTORY}/logs"
PID_FILE="${ARTIFACTS_DIRECTORY}/backend-processes.pid"

COMPOSE_COMMAND=(
  docker compose
  --file "${ROOT_DIRECTORY}/docker-compose.yml"
  --file "${ROOT_DIRECTORY}/docker-compose.e2e.yml"
)

POSTGRES_USERNAME="eshop"
POSTGRES_PASSWORD="eshop_password"
POSTGRES_HOST="localhost"
POSTGRES_PORT="5432"

CATALOG_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=catalog_db;Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"
ORDERS_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=orders_db;Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"
INVENTORY_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=inventory_db;Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"
PAYMENTS_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=payments_db;Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"
NOTIFICATIONS_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=notifications_db;Username=${POSTGRES_USERNAME};Password=${POSTGRES_PASSWORD}"

BACKEND_PROJECTS=(
  "src/backend/gateways/ApiGateway/ApiGateway.csproj"
  "src/backend/services/BasketService/BasketService.csproj"
  "src/backend/services/CatalogService/CatalogService.csproj"
  "src/backend/services/OrdersService/OrdersService.csproj"
  "src/backend/services/InventoryService/InventoryService.csproj"
  "src/backend/services/PaymentsService/PaymentsService.csproj"
  "src/backend/services/NotificationsService/NotificationsService.csproj"
  "src/backend/tools/RabbitMq.TopologyInitializer/RabbitMq.TopologyInitializer.csproj"
)

BACKEND_PORTS=(
  5080
  5081
  5082
  5083
  5084
  5085
  5086
)

declare -A SERVICE_PIDS=()
declare -A SERVICE_LOGS=()

cleanup_after_error() {
  local exit_code="$?"

  echo
  echo "E2E stack startup failed with exit code ${exit_code}."

  if compgen -G "${LOGS_DIRECTORY}/*.log" >/dev/null; then
    echo
    echo "Last backend log lines:"

    for log_file in "${LOGS_DIRECTORY}"/*.log; do
      echo
      echo "===== ${log_file} ====="

      tail -n 50 "${log_file}" ||
        true
    done
  fi

  "${ROOT_DIRECTORY}/scripts/e2e/stop-stack.sh" ||
    true

  exit "${exit_code}"
}

trap cleanup_after_error ERR

is_port_in_use() {
  local port="$1"

  if [[ "${OSTYPE:-}" == msys* ||
        "${OSTYPE:-}" == cygwin* ]]
  then
    netstat -ano 2>/dev/null |
      tr -d '\r' |
      grep -E \
        "[\.:]${port}[[:space:]].*LISTENING" \
        >/dev/null

    return
  fi

  if command -v ss >/dev/null 2>&1; then
    ss \
      --headers=never \
      --listening \
      --tcp \
      "sport = :${port}" |
      grep -q .

    return
  fi

  netstat -ltn 2>/dev/null |
    tr -d '\r' |
    grep -E \
      "[\.:]${port}[[:space:]].*LISTEN" \
      >/dev/null
}

print_backend_port_owners() {
  if [[ "${OSTYPE:-}" == msys* ||
        "${OSTYPE:-}" == cygwin* ]]
  then
    netstat -ano 2>/dev/null |
      tr -d '\r' |
      grep -E \
        ':(5080|5081|5082|5083|5084|5085|5086)[[:space:]].*LISTENING' ||
      true

    return
  fi

  if command -v ss >/dev/null 2>&1; then
    ss \
      --listening \
      --tcp \
      --processes \
      --numeric |
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

wait_for_url() {
  local name="$1"
  local url="$2"
  local maximum_attempts="${3:-60}"

  echo "Waiting for ${name}: ${url}"

  for ((
    attempt = 1;
    attempt <= maximum_attempts;
    attempt++
  )); do
    if curl \
      --fail \
      --silent \
      --output /dev/null \
      --connect-timeout 1 \
      --max-time 2 \
      "${url}"
    then
      echo "${name} is ready."
      return 0
    fi

    if ((attempt % 10 == 0)); then
      echo \
        "${name} is not ready after $((attempt * 2)) seconds."
    fi

    sleep 2
  done

  echo
  echo "${name} did not become ready."

  return 1
}

wait_for_service() {
  local service_key="$1"
  local display_name="$2"
  local url="$3"
  local maximum_attempts="${4:-60}"

  local process_id="${SERVICE_PIDS[$service_key]:-}"
  local log_file="${SERVICE_LOGS[$service_key]:-}"

  if [[ -z "${process_id}" ]]; then
    echo "No PID was registered for ${display_name}."
    return 1
  fi

  if [[ -z "${log_file}" ]]; then
    echo "No log file was registered for ${display_name}."
    return 1
  fi

  echo "Waiting for ${display_name}: ${url}"

  for ((
    attempt = 1;
    attempt <= maximum_attempts;
    attempt++
  )); do
    if ! kill -0 "${process_id}" 2>/dev/null; then
      echo
      echo "${display_name} exited before becoming ready."
      echo
      echo "===== ${log_file} ====="

      tail -n 200 "${log_file}" ||
        true

      return 1
    fi

    if curl \
      --fail \
      --silent \
      --output /dev/null \
      --connect-timeout 1 \
      --max-time 2 \
      "${url}"
    then
      echo "${display_name} is ready."
      return 0
    fi

    if ((attempt % 10 == 0)); then
      echo \
        "${display_name} is not ready after $((attempt * 2)) seconds."
    fi

    sleep 2
  done

  echo
  echo "${display_name} did not become ready."
  echo
  echo "===== ${log_file} ====="

  tail -n 200 "${log_file}" ||
    true

  return 1
}
wait_for_seeded_product() {
  local url="http://localhost:5173/api/v1/products"
  local product_name="E2E Mechanical Keyboard"
  local maximum_attempts="${1:-60}"

  echo "Waiting for seeded product through frontend proxy: ${url}"

  for ((
    attempt = 1;
    attempt <= maximum_attempts;
    attempt++
  )); do
    local response

    response="$(
      curl \
        --fail \
        --silent \
        --connect-timeout 1 \
        --max-time 3 \
        "${url}" 2>/dev/null ||
      true
    )"

    if grep \
      --fixed-strings \
      --quiet \
      "\"name\":\"${product_name}\"" \
      <<<"${response}"
    then
      echo "Seeded product is available through frontend proxy."
      return 0
    fi

    if ((attempt % 10 == 0)); then
      echo \
        "Seeded product is not available after $((attempt * 2)) seconds."
    fi

    sleep 2
  done

  echo
  echo "Seeded product was not returned through the frontend proxy."

  echo
  echo "Direct Gateway response:"

  curl \
    --silent \
    --show-error \
    "http://localhost:5080/api/v1/products" ||
    true

  echo
  echo
  echo "Frontend proxy response:"

  curl \
    --silent \
    --show-error \
    "http://localhost:5173/api/v1/products" ||
    true

  echo

  return 1
}

restore_and_build_project() {
  local project="$1"

  echo "Restoring ${project}..."

  dotnet restore \
    "${ROOT_DIRECTORY}/${project}"

  echo "Building ${project}..."

  dotnet build \
    "${ROOT_DIRECTORY}/${project}" \
    --configuration Release \
    --no-restore
}

migrate_database() {
  local name="$1"
  local project="$2"
  local connection_string="$3"

  echo "Applying ${name} migrations..."

  dotnet ef database update \
    --project "${ROOT_DIRECTORY}/${project}" \
    --startup-project "${ROOT_DIRECTORY}/${project}" \
    --configuration Release \
    --no-build \
    --connection "${connection_string}"
}

start_service() {
  local name="$1"
  local project="$2"

  shift 2

  local log_file="${LOGS_DIRECTORY}/${name}.log"

  : >"${log_file}"

  echo "Starting ${name}..."

  (
    export ASPNETCORE_ENVIRONMENT="E2E"
    export DOTNET_ENVIRONMENT="E2E"
    export OTEL_SDK_DISABLED="true"

    export Keycloak__Authority="http://localhost:18080/realms/eshop"
    export Keycloak__Audience="eshop-api"
    export Keycloak__RequireHttpsMetadata="false"

    export RabbitMq__HostName="localhost"
    export RabbitMq__Port="5672"
    export RabbitMq__UserName="eshop"
    export RabbitMq__Password="eshop_password"
    export RabbitMq__RequestedHeartbeatSeconds="30"
    export RabbitMq__AutomaticRecoveryEnabled="true"
    export RabbitMq__TopologyRecoveryEnabled="true"
    export RabbitMq__ConsumerDeliveryLimit="5"

    for assignment in "$@"; do
      export "${assignment}"
    done

    exec dotnet run \
      --project "${ROOT_DIRECTORY}/${project}" \
      --configuration Release \
      --no-build \
      --no-launch-profile
  ) >"${log_file}" 2>&1 &

  local process_id="$!"

  echo "${process_id}" >>"${PID_FILE}"

  SERVICE_PIDS["${name}"]="${process_id}"
  SERVICE_LOGS["${name}"]="${log_file}"

  echo "${name} started with PID ${process_id}."
}

cd "${ROOT_DIRECTORY}"

echo "Stopping previous E2E environment..."

"${ROOT_DIRECTORY}/scripts/e2e/stop-stack.sh"

mkdir -p "${LOGS_DIRECTORY}"

rm -f "${LOGS_DIRECTORY}"/*.log

assert_backend_ports_are_free

: >"${PID_FILE}"

echo "Starting infrastructure and frontend..."

"${COMPOSE_COMMAND[@]}" up \
  --detach \
  --wait \
  --wait-timeout 180 \
  postgres \
  redis \
  rabbitmq \
  keycloak \
  frontend

wait_for_url \
  "Keycloak" \
  "http://localhost:18080/realms/eshop/.well-known/openid-configuration" \
  60

wait_for_url \
  "Frontend" \
  "http://localhost:5173/" \
  60

echo "Restoring local .NET tools..."

dotnet tool restore

for project in "${BACKEND_PROJECTS[@]}"; do
  restore_and_build_project "${project}"
done

export Keycloak__Authority="http://localhost:18080/realms/eshop"
export Keycloak__Audience="eshop-api"
export Keycloak__RequireHttpsMetadata="false"

export RabbitMq__HostName="localhost"
export RabbitMq__Port="5672"
export RabbitMq__UserName="eshop"
export RabbitMq__Password="eshop_password"
export RabbitMq__ConsumerDeliveryLimit="5"

migrate_database \
  "Catalog" \
  "src/backend/services/CatalogService/CatalogService.csproj" \
  "${CATALOG_CONNECTION_STRING}"

migrate_database \
  "Orders" \
  "src/backend/services/OrdersService/OrdersService.csproj" \
  "${ORDERS_CONNECTION_STRING}"

migrate_database \
  "Inventory" \
  "src/backend/services/InventoryService/InventoryService.csproj" \
  "${INVENTORY_CONNECTION_STRING}"

migrate_database \
  "Payments" \
  "src/backend/services/PaymentsService/PaymentsService.csproj" \
  "${PAYMENTS_CONNECTION_STRING}"

migrate_database \
  "Notifications" \
  "src/backend/services/NotificationsService/NotificationsService.csproj" \
  "${NOTIFICATIONS_CONNECTION_STRING}"

echo "Seeding deterministic E2E product..."

"${COMPOSE_COMMAND[@]}" exec \
  --no-TTY \
  postgres \
  psql \
  --set ON_ERROR_STOP=1 \
  --username "${POSTGRES_USERNAME}" \
  --dbname catalog_db <<'SQL'
INSERT INTO products
(
    id,
    name,
    sku,
    description,
    category,
    price_amount,
    currency,
    is_active,
    created_at_utc,
    updated_at_utc
)
VALUES
(
    '10000000-0000-0000-0000-000000000001',
    'E2E Mechanical Keyboard',
    'E2E-KEYBOARD-001',
    'Deterministic product used by the Playwright checkout suite.',
    'E2E',
    2500.00,
    'CZK',
    TRUE,
    NOW(),
    NULL
);
SQL

echo "Seeding deterministic E2E inventory..."

"${COMPOSE_COMMAND[@]}" exec \
  --no-TTY \
  postgres \
  psql \
  --set ON_ERROR_STOP=1 \
  --username "${POSTGRES_USERNAME}" \
  --dbname inventory_db <<'SQL'
INSERT INTO inventory_items
(
    id,
    product_id,
    sku,
    on_hand_quantity,
    reserved_quantity,
    is_active,
    created_at_utc,
    updated_at_utc
)
VALUES
(
    '20000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'E2E-KEYBOARD-001',
    10,
    0,
    TRUE,
    NOW(),
    NULL
);
SQL

echo "Initializing RabbitMQ topology..."

env \
  RabbitMq__HostName="localhost" \
  RabbitMq__Port="5672" \
  RabbitMq__UserName="eshop" \
  RabbitMq__Password="eshop_password" \
  RabbitMq__ConsumerDeliveryLimit="5" \
  dotnet run \
    --project "${ROOT_DIRECTORY}/src/backend/tools/RabbitMq.TopologyInitializer/RabbitMq.TopologyInitializer.csproj" \
    --configuration Release \
    --no-build \
    --no-launch-profile

start_service \
  "catalog-service" \
  "src/backend/services/CatalogService/CatalogService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5081" \
  "ConnectionStrings__CatalogDb=${CATALOG_CONNECTION_STRING}"

start_service \
  "basket-service" \
  "src/backend/services/BasketService/BasketService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5082" \
  "ConnectionStrings__Redis=localhost:6379,abortConnect=false" \
  "Services__CatalogBaseUrl=http://127.0.0.1:5081/"

start_service \
  "orders-service" \
  "src/backend/services/OrdersService/OrdersService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5083" \
  "ConnectionStrings__OrdersDb=${ORDERS_CONNECTION_STRING}" \
  "Services__BasketBaseUrl=http://127.0.0.1:5082/"

start_service \
  "inventory-service" \
  "src/backend/services/InventoryService/InventoryService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5084" \
  "ConnectionStrings__InventoryDb=${INVENTORY_CONNECTION_STRING}"

start_service \
  "payments-service" \
  "src/backend/services/PaymentsService/PaymentsService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5085" \
  "ConnectionStrings__PaymentsDb=${PAYMENTS_CONNECTION_STRING}"

start_service \
  "notifications-service" \
  "src/backend/services/NotificationsService/NotificationsService.csproj" \
  "ASPNETCORE_URLS=http://127.0.0.1:5086" \
  "ConnectionStrings__NotificationsDb=${NOTIFICATIONS_CONNECTION_STRING}"

start_service \
  "api-gateway" \
  "src/backend/gateways/ApiGateway/ApiGateway.csproj" \
  "ASPNETCORE_URLS=http://0.0.0.0:5080"

wait_for_service \
  "catalog-service" \
  "Catalog Service" \
  "http://localhost:5081/health"

wait_for_service \
  "basket-service" \
  "Basket Service" \
  "http://localhost:5082/health"

wait_for_service \
  "orders-service" \
  "Orders Service" \
  "http://localhost:5083/health"

wait_for_service \
  "inventory-service" \
  "Inventory Service" \
  "http://localhost:5084/health"

wait_for_service \
  "payments-service" \
  "Payments Service" \
  "http://localhost:5085/health"

wait_for_service \
  "notifications-service" \
  "Notifications Service" \
  "http://localhost:5086/health"

wait_for_service \
  "api-gateway" \
  "API Gateway" \
  "http://localhost:5080/health" \
  90

wait_for_seeded_product 60

trap - ERR

echo
echo "E2E stack is ready:"
echo "  Frontend:   http://localhost:5173"
echo "  Gateway:    http://localhost:5080"
echo "  Keycloak:   http://localhost:18080"
echo "  Logs:       ${LOGS_DIRECTORY}"