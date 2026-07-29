#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/artifacts/delivery}"
mkdir -p "$OUT"
SOURCE_ZIP="$OUT/XLib.DockLayout-source.zip"
DELIVERY_ZIP="$OUT/XLib.DockLayout-delivery.zip"
rm -f "$SOURCE_ZIP" "$DELIVERY_ZIP" "$OUT/DELIVERY_SHA256SUMS.txt"

cd "$ROOT"
common_excludes=(
  '.git/*' '*/bin/*' '*/obj/*' '*/node_modules/*' '*/TestResults/*'
  '.vs/*' '.idea/*' '.playwright/*' '*/playwright/.cache/*' '*/playwright-report/*'
  '*.user' '*.suo'
)

zip -qr "$SOURCE_ZIP" . \
  -x "${common_excludes[@]}" \
  'artifacts/*'

zip -qr "$DELIVERY_ZIP" . \
  -x "${common_excludes[@]}" \
  'artifacts/packages/*' 'artifacts/playground/*' 'artifacts/test-results/*' \
  'artifacts/e2e-results/*' 'artifacts/delivery/*'

sha256sum "$SOURCE_ZIP" "$DELIVERY_ZIP" > "$OUT/DELIVERY_SHA256SUMS.txt"
unzip -t "$SOURCE_ZIP" >/dev/null
unzip -t "$DELIVERY_ZIP" >/dev/null
printf 'Created:\n%s\n%s\n' "$SOURCE_ZIP" "$DELIVERY_ZIP"
