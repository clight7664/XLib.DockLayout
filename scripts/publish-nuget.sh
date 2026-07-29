#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.1.0}"
MODE="${2:-pack}"
CONFIGURATION="${CONFIGURATION:-Release}"
SOURCE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT="$ROOT/artifacts/packages"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid version: $VERSION" >&2
  exit 2
fi
if [[ "$MODE" != "pack" && "$MODE" != "--push" ]]; then
  echo "Usage: $0 [version] [--push]" >&2
  exit 2
fi
if [[ "$MODE" == "--push" && -z "${NUGET_API_KEY:-}" ]]; then
  echo "NUGET_API_KEY is required with --push." >&2
  exit 2
fi

cd "$ROOT"
rm -rf "$OUTPUT"
mkdir -p "$OUTPUT"

dotnet restore XLib.DockLayout.sln
dotnet format XLib.DockLayout.sln --verify-no-changes --no-restore
dotnet build XLib.DockLayout.sln -c "$CONFIGURATION" --no-restore
dotnet test tests/XLib.DockLayout.Tests/XLib.DockLayout.Tests.csproj -c "$CONFIGURATION" --no-build
dotnet pack src/XLib.DockLayout/XLib.DockLayout.csproj \
  -c "$CONFIGURATION" \
  --no-build \
  -p:PackageVersion="$VERSION" \
  -o "$OUTPUT"

if [[ "$MODE" != "--push" ]]; then
  echo "Packages created in $OUTPUT. Re-run with --push and NUGET_API_KEY to publish."
  exit 0
fi

shopt -s nullglob
packages=("$OUTPUT"/*.nupkg "$OUTPUT"/*.snupkg)
if (( ${#packages[@]} == 0 )); then
  echo "No package files were produced." >&2
  exit 1
fi
for package in "${packages[@]}"; do
  dotnet nuget push "$package" \
    --api-key "$NUGET_API_KEY" \
    --source "$SOURCE" \
    --skip-duplicate
done
