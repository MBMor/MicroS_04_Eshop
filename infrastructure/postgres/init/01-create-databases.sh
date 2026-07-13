#!/usr/bin/env bash
set -euo pipefail

create_database_if_missing() {
  local database_name="$1"

  if psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --tuples-only --no-align \
      --command "SELECT 1 FROM pg_database WHERE datname = '$database_name';" | grep -q 1; then
    echo "Database '$database_name' already exists."
  else
    echo "Creating database '$database_name'."
    createdb --username "$POSTGRES_USER" "$database_name"
  fi
}

create_database_if_missing "$CATALOG_DB"
create_database_if_missing "$ORDERS_DB"
create_database_if_missing "$INVENTORY_DB"
create_database_if_missing "$PAYMENTS_DB"
create_database_if_missing "$NOTIFICATIONS_DB"
create_database_if_missing "$KEYCLOAK_DB"