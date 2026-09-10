#!/bin/bash
set -euo pipefail

: "${CONNECTION_STRING:?CONNECTION_STRING environment variable is required}"

echo "[migrate] $(date -u +%Y-%m-%dT%H:%M:%SZ) Starting EF Core migration bundles"

for bundle in /migrations/efbundle-*; do
    if [ -x "$bundle" ]; then
        name="$(basename "$bundle")"
        echo "[migrate] Applying $name"
        "$bundle" --connection "$CONNECTION_STRING"
    fi
done

echo "[migrate] $(date -u +%Y-%m-%dT%H:%M:%SZ) All migration bundles applied successfully"
