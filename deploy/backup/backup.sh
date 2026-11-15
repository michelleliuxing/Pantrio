#!/usr/bin/env bash
# Nightly Pantrio backup: pg_dump (custom format) streamed off the server with rclone.
# Nothing is kept on the server's disk.
#
# Requires: docker, rclone with a configured remote (run `rclone config` once as the cron user).
# Settings (env or /opt/pantrio/backup.env):
#   BACKUP_REMOTE   rclone destination, e.g. r2:pantrio-backups  or  gdrive:Backups/pantrio
#   KEEP_DAYS       delete remote dumps older than this (default 30)
#   DB_CONTAINER    default pantrio-db
#
# Cron (02:30 Sydney time, server clock in UTC => 15:30/16:30 UTC; or set CRON_TZ):
#   CRON_TZ=Australia/Sydney
#   30 2 * * * /opt/pantrio/backup/backup.sh >> /var/log/pantrio-backup.log 2>&1

set -euo pipefail

if [[ -f /opt/pantrio/backup.env ]]; then
  # shellcheck disable=SC1091
  source /opt/pantrio/backup.env
fi

: "${BACKUP_REMOTE:?Set BACKUP_REMOTE (rclone remote:path)}"
KEEP_DAYS="${KEEP_DAYS:-30}"
DB_CONTAINER="${DB_CONTAINER:-pantrio-db}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
NAME="pantrio-${STAMP}.dump"

echo "[$(date -u +%FT%TZ)] backing up ${DB_CONTAINER} to ${BACKUP_REMOTE}/${NAME}"

# --format=custom is compressed and restorable with pg_restore (selective, parallel).
docker exec "${DB_CONTAINER}" pg_dump --username=pantrio --dbname=pantrio --format=custom --no-owner --no-privileges \
  | rclone rcat "${BACKUP_REMOTE}/${NAME}"

# Fail loudly if the upload is missing or suspiciously small.
SIZE="$(rclone size --json "${BACKUP_REMOTE}/${NAME}" | sed -E 's/.*"bytes":([0-9]+).*/\1/')"
if [[ -z "${SIZE}" || "${SIZE}" -lt 1024 ]]; then
  echo "backup looks wrong: ${SIZE:-0} bytes" >&2
  exit 1
fi

rclone delete --min-age "${KEEP_DAYS}d" --include "pantrio-*.dump" "${BACKUP_REMOTE}"
echo "[$(date -u +%FT%TZ)] done (${SIZE} bytes); kept the last ${KEEP_DAYS} days"
