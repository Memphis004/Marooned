#!/usr/bin/env bash
# Copies Shared/*.cs into Marooned and McpBridge. Edit files ONLY in the
# top-level Shared/ folder — the copies below get overwritten every run.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$ROOT_DIR/Shared"

UNITY_DEST="$ROOT_DIR/Marooned/Assets/Scripts/Shared"
BRIDGE_DEST="$ROOT_DIR/McpBridge/Shared"

mkdir -p "$UNITY_DEST" "$BRIDGE_DEST"

cp "$SRC"/*.cs "$UNITY_DEST"/
cp "$SRC"/*.cs "$BRIDGE_DEST"/

echo "Synced $(ls "$SRC"/*.cs | wc -l | tr -d ' ') file(s) from Shared/ -> Marooned/ and McpBridge/"
