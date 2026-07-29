#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
mkdir -p artifacts/logs artifacts/test-results artifacts/playground artifacts/packages
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet --info | tee artifacts/logs/dotnet-info.log
dotnet restore XLib.DockLayout.sln | tee artifacts/logs/restore.log
dotnet restore XLib.DockLayout.sln --locked-mode | tee artifacts/logs/restore-locked.log
dotnet format XLib.DockLayout.sln --verify-no-changes | tee artifacts/logs/format.log
dotnet build XLib.DockLayout.sln -c Release --no-restore | tee artifacts/logs/build-release.log
dotnet test XLib.DockLayout.sln -c Release --no-build --logger "trx;LogFileName=test-results.trx" --results-directory artifacts/test-results | tee artifacts/logs/test.log
dotnet publish samples/DockLayout.Playground/DockLayout.Playground.csproj -c Release --no-build -o artifacts/playground | tee artifacts/logs/publish.log
dotnet pack src/XLib.DockLayout/XLib.DockLayout.csproj -c Release --no-build -o artifacts/packages | tee artifacts/logs/pack.log
