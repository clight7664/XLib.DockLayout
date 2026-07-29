[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Source = 'https://api.nuget.org/v3/index.json',

    [Parameter()]
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts/packages'

if ($Push -and [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw 'NUGET_API_KEY is required when -Push is specified.'
}

Push-Location $root
try {
    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }
    New-Item $output -ItemType Directory -Force | Out-Null

    dotnet restore XLib.DockLayout.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet format XLib.DockLayout.sln --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet format failed.' }

    dotnet build XLib.DockLayout.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    dotnet test tests/XLib.DockLayout.Tests/XLib.DockLayout.Tests.csproj -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

    dotnet pack src/XLib.DockLayout/XLib.DockLayout.csproj `
        -c $Configuration `
        --no-build `
        -p:PackageVersion=$Version `
        -o $output
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    if (-not $Push) {
        Write-Host "Packages created in $output. Re-run with -Push and NUGET_API_KEY to publish."
        return
    }

    $packages = Get-ChildItem $output -File | Where-Object { $_.Extension -in @('.nupkg', '.snupkg') }
    if ($packages.Count -eq 0) { throw 'No package files were produced.' }
    foreach ($package in $packages) {
        dotnet nuget push $package.FullName `
            --api-key $env:NUGET_API_KEY `
            --source $Source `
            --skip-duplicate
        if ($LASTEXITCODE -ne 0) { throw "NuGet push failed for $($package.Name)." }
    }
}
finally {
    Pop-Location
}
