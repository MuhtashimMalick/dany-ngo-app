<#
.SYNOPSIS
    Publishes NgoFund.Desktop as a self-contained, single-file win-x64 executable for
    distribution - no .NET runtime install required on the target machine.

.EXAMPLE
    .\scripts\publish-desktop.ps1
    .\scripts\publish-desktop.ps1 -OutputDir C:\builds\ngofund-desktop
#>
param(
    [string]$OutputDir = ".\publish\NgoFund.Desktop"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet publish "$repoRoot\src\NgoFund.Desktop\NgoFund.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Published to $OutputDir"
Write-Host "To point this build at a non-default API host, drop an appsettings.json next to" -ForegroundColor Yellow
Write-Host "NgoFund.Desktop.exe there - see docs/deployment.md." -ForegroundColor Yellow
