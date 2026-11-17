# Publish-Release.ps1 - sync sources, build, test, package and publish the Displeased Crucible plugins.
#
#   .\Publish-Release.ps1                 # full run: sync, build, test, zip, commit, push, GitHub release
#   .\Publish-Release.ps1 -SkipBuild      # reuse existing bin\Release outputs (still syncs + packages)
#   .\Publish-Release.ps1 -NoPush         # do everything locally, no commit/push/release
#   .\Publish-Release.ps1 -Notes "text"   # release notes
#
# Working sources stay where they are (see notes\CLAUDE.md). This repo holds a snapshot of them plus
# the built zips (as GitHub release assets) and pluginmaster.json, which Dalamud reads as a custom repo:
#   https://raw.githubusercontent.com/<owner>/displeased-crucible/main/pluginmaster.json
param([switch]$SkipBuild, [switch]$NoPush, [string]$Notes = '')
$ErrorActionPreference = 'Stop'

$Repo    = $PSScriptRoot
$RepoName = 'displeased-crucible'
$W       = 'C:\Users\earth\Documents\Codex\2026-11-03\is-x20\work'
$BmrRoot = 'E:\Dalamud_Plugins\development\work\BossmodReborn-main'
$Live    = 'E:\Dalamud_Plugins'

$Plugins = @(
    @{ Name = 'DispleasedBeastHelper'; Src = "$W\BeastHelper"; Dest = 'src\BeastHelper'
       Proj = "$W\BeastHelper\src\BeastNav\BeastNav.csproj"; Bin = "$W\BeastHelper\src\BeastNav\bin\Release"
       Files = @('DispleasedBeastHelper.dll','DispleasedBeastHelper.json','DispleasedBeastHelper.deps.json','MoonSharp.Interpreter.dll')
       License = "$W\BeastHelper\LICENSE"; Icon = 'src/BeastHelper/images/icon.png'; LiveDir = "$Live\beasthelper" },
    @{ Name = 'RotationSolver'; Src = "$W\RotationSolverReborn-main"; Dest = 'src\RotationSolverReborn'
       Proj = "$W\RotationSolverReborn-main\RotationSolver\RotationSolver.csproj"; Bin = "$W\RotationSolverReborn-main\RotationSolver\bin\Release"
       Files = @('RotationSolver.dll','RotationSolver.json','RotationSolver.deps.json','ECommons.dll','RebornMaterial.dll')
       License = "$W\RotationSolverReborn-main\COPYING"; Icon = $null; LiveDir = "$Live\RSR-FirstBoard-Assist" },
    @{ Name = 'BossModReborn'; Src = $BmrRoot; Dest = 'src\BossModReborn'
       Proj = "$BmrRoot\BossMod\BossModReborn.csproj"; Bin = "$BmrRoot\BossMod\bin\Release"
       Files = @('BossModReborn.dll','BossModReborn.json','BossModReborn.pdb','DefaultRotationPresets.json','RebornPresets.json','RebornMaterial.dll')
       License = "$BmrRoot\LICENSE"; Icon = $null; LiveDir = "$Live\Resurrection-BMR-Test1" }
)

function Invoke-Robo([string]$From, [string]$To, [string[]]$Extra) {
    & robocopy $From $To /MIR /NFL /NDL /NJH /NJS /NP /XD bin obj .vs .git __pycache__ /XF .git *.user @Extra | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE): $From -> $To" }
    $global:LASTEXITCODE = 0
}

# 1. Snapshot sources and notes -------------------------------------------------------------------
Write-Host '== Syncing sources' -ForegroundColor Cyan
foreach ($p in $Plugins) { Invoke-Robo $p.Src (Join-Path $Repo $p.Dest) @() }
foreach ($p in $Plugins) {
    $nd = Join-Path $Repo ("notes\" + $p.Name); New-Item -ItemType Directory -Force $nd | Out-Null
    Get-ChildItem "$($p.LiveDir)\*" -File -Include *.txt,*.md | Copy-Item -Destination $nd -Force
}
Copy-Item "$Live\CLAUDE.md" (Join-Path $Repo 'notes\CLAUDE.md') -Force
Invoke-Robo "$Live\development\replay-tools" (Join-Path $Repo 'tools\replay-tools') @()

# 2. Build and test -------------------------------------------------------------------------------
$env:DOTNET_CLI_HOME = "$W\dotnet-home"; $env:NUGET_PACKAGES = "$W\nuget-packages"
$env:DOTNET_NOLOGO = 1; $env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DALAMUD_HOME = "$env:APPDATA\XIVLauncher\addon\Hooks\dev"
$dotnet = "$W\dotnet\dotnet.exe"; $cfg = @('-c','Release',"-p:RestoreConfigFile=$W\NuGet.Config",'-nologo','-v','q')
if (-not $SkipBuild) {
    Write-Host '== Policy tests' -ForegroundColor Cyan
    & $dotnet build "$W\BeastHelper\tests\FirstBoardScore\FirstBoardScore.csproj" @cfg
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    $out = & $dotnet "$W\BeastHelper\tests\FirstBoardScore\bin\Release\net10.0\FirstBoardScore.dll"
    $out | Select-Object -Last 3 | Write-Host
    if (-not ($out -match '^PASS')) { throw 'Policy tests did not PASS.' }
    foreach ($p in $Plugins) {
        Write-Host "== Building $($p.Name)" -ForegroundColor Cyan
        & $dotnet build $p.Proj @cfg
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $($p.Name)" }
    }
}

# 3. Package --------------------------------------------------------------------------------------
$owner = if ($NoPush) { 'OWNER' } else { (& gh api user -q .login).Trim() }
$tag   = 'release-' + (Get-Date -Format 'yyyyMMdd-HHmm')
$raw   = "https://raw.githubusercontent.com/$owner/$RepoName/main"
$dist  = Join-Path $Repo "dist\$tag"; New-Item -ItemType Directory -Force $dist | Out-Null
$master = @()
foreach ($p in $Plugins) {
    $stage = Join-Path $dist $p.Name; New-Item -ItemType Directory -Force $stage | Out-Null
    foreach ($f in $p.Files) { Copy-Item (Join-Path $p.Bin $f) $stage -ErrorAction Stop }
    Copy-Item $p.License (Join-Path $stage 'LICENSE.txt')
    $zip = Join-Path $dist ($p.Name + '.zip')
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    Remove-Item $stage -Recurse -Force

    $m = Get-Content (Join-Path $p.Bin ($p.Name + '.json')) -Raw -Encoding UTF8 | ConvertFrom-Json
    $dl = "https://github.com/$owner/$RepoName/releases/download/$tag/$($p.Name).zip"
    $m | Add-Member -Force NoteProperty RepoUrl "https://github.com/$owner/$RepoName"
    if ($p.Icon) { $m | Add-Member -Force NoteProperty IconUrl "$raw/$($p.Icon)" }
    $m | Add-Member -Force NoteProperty DownloadLinkInstall $dl
    $m | Add-Member -Force NoteProperty DownloadLinkUpdate $dl
    $m | Add-Member -Force NoteProperty DownloadLinkTesting $dl
    $m | Add-Member -Force NoteProperty IsHide $false
    $m | Add-Member -Force NoteProperty IsTestingExclusive $false
    $m | Add-Member -Force NoteProperty LastUpdate ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
    $master += $m
    Write-Host ("   {0} {1} -> {2}" -f $p.Name, $m.AssemblyVersion, $zip)
}
$json = ConvertTo-Json -InputObject @($master) -Depth 6
[IO.File]::WriteAllText((Join-Path $Repo 'pluginmaster.json'), $json, (New-Object Text.UTF8Encoding $false))

if ($NoPush) { Write-Host "== Local only. Zips in $dist" -ForegroundColor Yellow; return }

# 4. Commit, push, release ------------------------------------------------------------------------
Push-Location $Repo
try {
    git add -A
    git commit -m "Release $tag" | Out-Null
    git push origin main
    if ($LASTEXITCODE -ne 0) { throw 'git push failed.' }
    $vers = ($master | ForEach-Object { "$($_.InternalName) $($_.AssemblyVersion)" }) -join ', '
    $body = if ($Notes) { "$Notes`n`n$vers" } else { $vers }
    # Notes go through a file: PowerShell 5.1 mangles embedded quotes in native-command arguments.
    $notesFile = Join-Path $dist 'release-notes.txt'
    [IO.File]::WriteAllText($notesFile, $body, (New-Object Text.UTF8Encoding $false))
    & gh release create $tag (Get-ChildItem $dist -Filter *.zip).FullName --title $tag --notes-file $notesFile --target main
    if ($LASTEXITCODE -ne 0) { throw 'gh release create failed.' }
    Write-Host "== Published $tag" -ForegroundColor Green
    Write-Host "   Custom repo URL: $raw/pluginmaster.json"
} finally { Pop-Location }
