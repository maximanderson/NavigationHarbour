# Builds Portal Core for standalone hosting (see build-standalone.sh for the layout and the reasoning).
#
# Usage: scripts\build-standalone.ps1 [-Windows] [-Out <output-directory>]     (default: artifacts\PortalCore)
#   -Windows   Windows x64 self-contained package (no IIS, no installed .NET runtime). Not single-file, not trimmed.
#   -Release -Version X.Y.Z   (with -Windows) the shareable package: demonstration data only, an explicit file
#              allowlist, a safety scan, and artifacts\release\PortalCore-win-x64-vX.Y.Z.zip + .sha256.
param(
  [switch]$Windows,
  [switch]$Release,
  [string]$Version = "",
  [string]$Out = ""
)
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

if ($Release) {
  if (-not $Windows) { throw "-Release requires -Windows (the release package is Windows x64 self-contained)." }
  if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$') { throw "-Release requires -Version <major.minor.patch>, for example -Version 1.0.0" }
  if (-not $Out) { $Out = Join-Path $root "artifacts\release" }
  . (Join-Path $PSScriptRoot "release-package.ps1")
  Build-Release -Root $root -Out $Out -Version $Version
  return
}

if (-not $Out) { $Out = Join-Path $root "artifacts\PortalCore" }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$project = Join-Path $root "backend\PortalCore.Api"

Write-Host "== Frontend build"
Push-Location (Join-Path $root "frontend")
try {
  if (-not (Test-Path "node_modules")) { npm ci; if ($LASTEXITCODE) { throw "npm ci failed" } }
  npm run build; if ($LASTEXITCODE) { throw "npm run build failed" }

  Write-Host "== Backend publish -> $Out\bin"
  if ($Windows) {
    dotnet publish (Join-Path $project "PortalCore.Api.csproj") -c Release -r win-x64 --self-contained true -o (Join-Path $Out "bin")
  } else {
    dotnet publish (Join-Path $project "PortalCore.Api.csproj") -c Release -o (Join-Path $Out "bin")
  }
  if ($LASTEXITCODE) { throw "dotnet publish failed" }

  # The package must never contain a database.
  $databases = Get-ChildItem -Path (Join-Path $Out "bin") -Recurse -File -Include "*.db", "*.db-wal", "*.db-shm"
  if ($databases) { throw "A SQLite database file was found in $Out\bin; refusing to continue." }

  Write-Host "== Frontend -> $Out\bin\wwwroot"
  $www = Join-Path $Out "bin\wwwroot"
  if (Test-Path $www) { Remove-Item -Recurse -Force $www }
  Copy-Item -Recurse "dist" $www
  foreach ($folder in "config", "navigation", "content") {
    $path = Join-Path $www $folder
    if (Test-Path $path) { Remove-Item -Recurse -Force $path }
  }

  Write-Host "== Directories"
  foreach ($folder in "conf", "data", "logs", "backups", "certs", "ai", "docs") {
    New-Item -ItemType Directory -Force -Path (Join-Path $Out $folder) | Out-Null
  }
  Copy-Item (Join-Path $project "conf\server.sample.ini") (Join-Path $Out "conf\server.sample.ini") -Force   # never server.ini

  $public = Join-Path $Out "public"
  if (-not (Test-Path $public)) {
    Write-Host "== Initial public files -> $public"
    New-Item -ItemType Directory -Path $public | Out-Null
    foreach ($folder in "navigation", "content") {
      if (Test-Path "public\$folder") { Copy-Item -Recurse "public\$folder" (Join-Path $public $folder) }
    }
    New-Item -ItemType Directory -Path (Join-Path $public "config") | Out-Null
    Copy-Item (Join-Path $root "deploy\portal.standalone.ini") (Join-Path $public "config\portal.ini")   # same-origin API settings
  } else {
    Write-Host "== $public exists; left unchanged"
  }

  $ai = Join-Path $Out "ai"
  if (-not (Get-ChildItem -Path $ai -Force)) {
    Write-Host "== Sample AI knowledge and prompts -> $ai"
    Copy-Item -Recurse (Join-Path $project "Data\knowledge") (Join-Path $ai "knowledge")
    Copy-Item -Recurse (Join-Path $project "Data\prompts") (Join-Path $ai "prompts")
  }

  Write-Host "== Documentation -> $Out\docs"
  foreach ($doc in "windows-deployment", "standalone-hosting", "server-configuration", "backup-restore", "logging") {
    $source = Join-Path $root "docs\$doc.md"
    if (Test-Path $source) { Copy-Item $source (Join-Path $Out "docs\$doc.md") -Force }
  }
}
finally { Pop-Location }

Write-Host "Done: $Out"
Write-Host "Next: copy conf\server.sample.ini to conf\server.ini and edit it, then run: PortalCore.Api --home <home> --init-db"
