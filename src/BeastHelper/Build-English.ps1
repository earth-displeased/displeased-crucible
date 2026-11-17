param([Parameter(Mandatory=$true)][string]$DalamudPath)
$ErrorActionPreference = 'Stop'
$env:DALAMUD_HOME = (Resolve-Path -LiteralPath $DalamudPath).Path
Push-Location $PSScriptRoot
try {
    dotnet build src/BeastNav/BeastNav.csproj -c Release -p:Use_DalamudPackager=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Package: src/BeastNav/bin/Release/BeastHelper/latest.zip'
} finally { Pop-Location }
