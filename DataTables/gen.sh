#!/usr/bin/env bash
set -euo pipefail
WORKSPACE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GEN_CLIENT="$WORKSPACE/Tools/Luban/Luban.dll"
CONF_ROOT="$WORKSPACE/DataTables"

dotnet "$GEN_CLIENT" \
    -t client \
    -c cs-simple-json \
    -d json \
    --conf "$CONF_ROOT/luban.conf" \
    -x outputCodeDir="$WORKSPACE/Marooned/Assets/Scripts/Data/Gen" \
    -x outputDataDir="$WORKSPACE/Marooned/Assets/Resources/DataTables"

echo ""
echo "Done. Regenerated code into Marooned/Assets/Scripts/Data/Gen"
echo "and data into Marooned/Assets/Resources/DataTables."
