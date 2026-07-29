#!/usr/bin/env bash
set -euo pipefail
dotnet build tests/DockLayout.Playground.E2E/DockLayout.Playground.E2E.csproj -c Release
pwsh tests/DockLayout.Playground.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
