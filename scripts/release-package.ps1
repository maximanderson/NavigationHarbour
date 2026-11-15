# Dot-sourced by build-standalone.ps1 when -Release is used. PowerShell port of release-package.sh (same allowlist,
# same rules file). NOT yet run on Windows or PowerShell: see the Stage 7 report.
#
# Safety in two layers: (1) ALLOWLIST - only the files named here are copied; (2) SCAN - every path and text file is
# checked against scripts\release-scan-rules.txt and a hit stops the build before any ZIP is written.

$ReleaseDocs = @("release-package", "windows-deployment", "server-configuration", "standalone-hosting", "backup-restore", "logging")

. (Join-Path $PSScriptRoot "release-scan.ps1")

function Build-Release([string]$Root, [string]$Out, [string]$Version) {
  New-Item -ItemType Directory -Force -Path $Out | Out-Null
  $Out = (Resolve-Path $Out).Path
  $project = Join-Path $Root "backend\PortalCore.Api"
  $stage = Join-Path $Out "stage"
  $pkg = Join-Path $stage "PortalCore"
  $name = "PortalCore-win-x64-v$Version"
  $zip = Join-Path $Out "$name.zip"
  foreach ($f in @($zip, "$zip.sha256")) { if (Test-Path $f) { Remove-Item $f -Force } }
  if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
  New-Item -ItemType Directory -Force -Path $pkg | Out-Null

  Write-Host "== Frontend build"
  Push-Location (Join-Path $Root "frontend")
  try {
    if (-not (Test-Path "node_modules")) { npm ci; if ($LASTEXITCODE) { throw "npm ci failed" } }
    npm run build; if ($LASTEXITCODE) { throw "npm run build failed" }
  } finally { Pop-Location }

  Write-Host "== Backend publish (win-x64, self-contained, no .pdb)"
  dotnet publish (Join-Path $project "PortalCore.Api.csproj") -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $pkg "bin")
  if ($LASTEXITCODE) { throw "dotnet publish failed" }

  Write-Host "== Frontend -> bin\wwwroot (allowlist)"
  $www = Join-Path $pkg "bin\wwwroot"
  New-Item -ItemType Directory -Force -Path $www | Out-Null
  $dist = Join-Path $Root "frontend\dist"
  Copy-Item (Join-Path $dist "index.html") $www
  Copy-Item -Recurse (Join-Path $dist "assets") (Join-Path $www "assets")
  foreach ($f in "favicon.svg", "icons.svg") { Copy-Item (Join-Path $dist $f) $www }

  Write-Host "== Directories"
  foreach ($d in "conf", "data", "logs", "backups", "certs", "ai", "docs", "public") { New-Item -ItemType Directory -Force -Path (Join-Path $pkg $d) | Out-Null }
  foreach ($d in "data", "logs", "backups", "certs") {
    [System.IO.File]::WriteAllText((Join-Path $pkg "$d\README.txt"), "Portal Core creates and uses files here. Keep this folder.`r`n")
  }
  Copy-Item (Join-Path $project "conf\server.sample.ini") (Join-Path $pkg "conf\server.sample.ini")

  Write-Host "== Demonstration data (allowlist)"
  $demo = Join-Path $Root "demo"
  foreach ($d in "public\config", "public\navigation", "public\content", "ai\knowledge\csv", "ai\prompts\csv") { New-Item -ItemType Directory -Force -Path (Join-Path $pkg $d) | Out-Null }
  Copy-Item (Join-Path $demo "config\portal.ini")                (Join-Path $pkg "public\config\portal.ini")
  Copy-Item (Join-Path $demo "navigation\demo-navigation.json")  (Join-Path $pkg "public\navigation\demo-navigation.json")
  Copy-Item (Join-Path $demo "navigation\demo-branches.json")    (Join-Path $pkg "public\navigation\demo-branches.json")
  Copy-Item (Join-Path $demo "content\reports.html")             (Join-Path $pkg "public\content\reports.html")
  Copy-Item (Join-Path $demo "ai\knowledge\csv\cities.csv")      (Join-Path $pkg "ai\knowledge\csv\cities.csv")
  Copy-Item (Join-Path $demo "ai\prompts\csv\contextPrompt.txt") (Join-Path $pkg "ai\prompts\csv\contextPrompt.txt")

  Write-Host "== Documentation (allowlist)"
  foreach ($d in $ReleaseDocs) { Copy-Item (Join-Path $Root "docs\$d.md") (Join-Path $pkg "docs\$d.md") }
  Copy-Item (Join-Path $Root "deploy\START-HERE.txt") (Join-Path $pkg "START-HERE.txt")
  [System.IO.File]::WriteAllText((Join-Path $pkg "VERSION.txt"), "Portal Core $Version (win-x64, self-contained)`r`n")

  Write-Host "== Safety scan (rules: scripts\release-scan-rules.txt)"
  Invoke-ReleaseScan -Stage $stage -Root $Root

  Write-Host "== ZIP -> $zip"
  Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
  $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
  [System.IO.File]::WriteAllText("$zip.sha256", "$hash  $name.zip`n")
  Remove-Item -Recurse -Force $stage
  Write-Host "Done."
  Write-Host ("  {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
  Write-Host "  $hash  $name.zip"
}
