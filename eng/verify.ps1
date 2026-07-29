$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
New-Item -ItemType Directory -Force artifacts/logs, artifacts/test-results, artifacts/playground, artifacts/packages | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
dotnet --info | Tee-Object artifacts/logs/dotnet-info.log
dotnet restore XLib.DockLayout.sln | Tee-Object artifacts/logs/restore.log
dotnet restore src/XLib.DockLayout/XLib.DockLayout.csproj --locked-mode | Tee-Object artifacts/logs/restore-locked-runtime.log
dotnet restore tests/XLib.DockLayout.Tests/XLib.DockLayout.Tests.csproj --locked-mode | Tee-Object artifacts/logs/restore-locked-tests.log
dotnet restore tests/DockLayout.Playground.E2E/DockLayout.Playground.E2E.csproj --locked-mode | Tee-Object artifacts/logs/restore-locked-e2e.log
dotnet format XLib.DockLayout.sln --verify-no-changes | Tee-Object artifacts/logs/format.log
dotnet build XLib.DockLayout.sln -c Release --no-restore | Tee-Object artifacts/logs/build-release.log
dotnet test XLib.DockLayout.sln -c Release --no-build --logger "trx;LogFileName=test-results.trx" --results-directory artifacts/test-results | Tee-Object artifacts/logs/test.log
dotnet publish samples/DockLayout.Playground/DockLayout.Playground.csproj -c Release --no-build -o artifacts/playground | Tee-Object artifacts/logs/publish.log
dotnet pack src/XLib.DockLayout/XLib.DockLayout.csproj -c Release --no-build -o artifacts/packages | Tee-Object artifacts/logs/pack.log
