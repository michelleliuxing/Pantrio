#!/usr/bin/env bash
# Restore a Pantrio dump into a PostgreSQL container.
#
#   restore.sh <dump file or rclone remote:path> [db-container]
#
# Examples:
#   restore.sh r2:pantrio-backups/pantrio-20261201T163000Z.dump              # into the live pantrio-db (asks first)
#   restore.sh ./pantrio-20261201T163000Z.dump pantrio-restore-test           # into a scratch container
#
# The target database is dropped and recreated, then the dump is restored into it.
# Stop the API first when restoring into the live database:
#   docker compose -f /opt/pantrio/compose.prod.yml stop pantrio-api

set -euo pipefail

SOURCE="${1:?usage: restore.sh <dump|remote:path> [db-container]}"
DB_CONTAINER="${2:-pantrio-db}"

if [[ "${DB_CONTAINER}" == "pantrio-db" ]]; then
  read -r -p "This replaces ALL data in the live pantrio database. Type 'restore' to continue: " answer
  [[ "${answer}" == "restore" ]] || { echo "aborted"; exit 1; }
fi

TMP="$(mktemp)"
trap 'rm -f "${TMP}"' EXIT

if [[ -f "${SOURCE}" ]]; then
  cp "${SOURCE}" "${TMP}"
else
  rclone copyto "${SOURCE}" "${TMP}"
fi

echo "restoring $(du -h "${TMP}" | cut -f1) into ${DB_CONTAINER}"
docker exec -i "${DB_CONTAINER}" psql --username=pantrio --dbname=postgres -v ON_ERROR_STOP=1 <<'SQL'
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'pantrio' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS pantrio;
CREATE DATABASE pantrio OWNER pantrio;
SQL
docker exec -i "${DB_CONTAINER}" pg_restore --username=pantrio --dbname=pantrio --no-owner --no-privileges --exit-on-error < "${TMP}"

echo "restored. Row counts:"
docker exec -i "${DB_CONTAINER}" psql --username=pantrio --dbname=pantrio -At <<'SQL'
SELECT 'households', count(*) FROM household
UNION ALL SELECT 'recipes', count(*) FROM recipe
UNION ALL SELECT 'plan entries', count(*) FROM meal_plan_entry
UNION ALL SELECT 'list items', count(*) FROM shopping_list_item;
SQL
