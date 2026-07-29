[CmdletBinding()]
param([string]$Output = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root 'artifacts/delivery'
}
New-Item -ItemType Directory -Force $Output | Out-Null

$sourceZip = Join-Path $Output 'XLib.DockLayout-source.zip'
$deliveryZip = Join-Path $Output 'XLib.DockLayout-delivery.zip'
$hashFile = Join-Path $Output 'DELIVERY_SHA256SUMS.txt'
Remove-Item $sourceZip, $deliveryZip, $hashFile -Force -ErrorAction SilentlyContinue

$blockedDirectoryNames = @('.git', 'bin', 'obj', 'node_modules', 'TestResults', '.vs', '.idea', '.playwright', 'playwright-report')
$blockedArtifactRoots = @('packages', 'playground', 'test-results', 'e2e-results', 'delivery')

function New-RepositoryStage {
    param(
        [Parameter(Mandatory)] [string]$Destination,
        [Parameter(Mandatory)] [bool]$IncludeEvidence
    )

    New-Item -ItemType Directory -Force $Destination | Out-Null
    Get-ChildItem $root -File -Recurse -Force | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
        $parts = $relative -split '[\\/]'
        if ($parts | Where-Object { $_ -in $blockedDirectoryNames }) { return }
        if ($_.Extension -in @('.user', '.suo')) { return }

        if ($parts[0] -eq 'artifacts') {
            if (-not $IncludeEvidence) { return }
            if ($parts.Count -gt 1 -and $parts[1] -in $blockedArtifactRoots) { return }
        }

        $target = Join-Path $Destination $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
        Copy-Item $_.FullName $target -Force
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('xdl-pack-' + [Guid]::NewGuid().ToString('N'))
try {
    $sourceStage = Join-Path $tempRoot 'source'
    $deliveryStage = Join-Path $tempRoot 'delivery'
    New-RepositoryStage -Destination $sourceStage -IncludeEvidence $false
    New-RepositoryStage -Destination $deliveryStage -IncludeEvidence $true
    [IO.Compression.ZipFile]::CreateFromDirectory($sourceStage, $sourceZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    [IO.Compression.ZipFile]::CreateFromDirectory($deliveryStage, $deliveryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Get-Item $sourceZip, $deliveryZip |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } |
    Set-Content $hashFile -Encoding utf8NoBOM

Write-Host "Created $sourceZip"
Write-Host "Created $deliveryZip"
