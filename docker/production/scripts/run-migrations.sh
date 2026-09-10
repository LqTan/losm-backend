#!/bin/bash
set -euo pipefail

: "${CONNECTION_STRING:?CONNECTION_STRING environment variable is required}"

SERVER=$(echo "$CONNECTION_STRING" | sed -n 's/.*Server=\([^;]*\).*/\1/p')
USER=$(echo   "$CONNECTION_STRING" | sed -n 's/.*User Id=\([^;]*\).*/\1/p')
PASS=$(echo   "$CONNECTION_STRING" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
DB=$(echo     "$CONNECTION_STRING" | sed -n 's/.*Database=\([^;]*\).*/\1/p')

echo "[migrate] $(date -u +%H:%M:%SZ) Server=$SERVER DB=$DB User=$USER"

echo "[migrate] Ensuring database [$DB] exists"
sqlcmd -S "$SERVER" -U "$USER" -P "$PASS" -C \
  -Q "IF DB_ID('$DB') IS NULL CREATE DATABASE [$DB]"

echo "[migrate] Applying EF Core migration bundles"
for bundle in /migrations/efbundle-*; do
    [ -x "$bundle" ] || continue
    name="$(basename "$bundle")"
    echo "[migrate] -> $name"
    "$bundle" --connection "$CONNECTION_STRING"
done

echo "[migrate] All migrations applied successfully"
