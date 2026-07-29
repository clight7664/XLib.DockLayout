$ErrorActionPreference = "Stop"
dotnet build tests/DockLayout.Playground.E2E/DockLayout.Playground.E2E.csproj -c Release
& tests/DockLayout.Playground.E2E/bin/Release/net10.0/playwright.ps1 install chromium
