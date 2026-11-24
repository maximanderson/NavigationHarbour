# Dot-sourced by release-package.ps1. PowerShell port of release_scan() in release-package.sh: the same rules file.
# Reports file names and line numbers only, never the matched text (it could be a secret).

# The one top-level folder inside the ZIP (the technical product name). Set by Build-Release; every path below,
# including the safety scan, is derived from it. Empty means the scan was not set up: it then stops instead of passing.
$script:PackageDir = ""

function Invoke-ReleaseScan([string]$Stage, [string]$Root) {
  $rules = Join-Path $Root "scripts\release-scan-rules.txt"
  if (-not (Test-Path $rules)) { throw "RELEASE BUILD STOPPED: scan rules not found: $rules" }
  if ([string]::IsNullOrWhiteSpace($script:PackageDir)) { throw "RELEASE BUILD STOPPED: the package folder name is not set; nothing could be scanned." }
  $pkg = Join-Path $Stage $script:PackageDir
  # Guard: a renamed or missing package folder must never make the scan pass by finding nothing to scan.
  if (-not (Test-Path $pkg -PathType Container)) { throw "RELEASE BUILD STOPPED: the package folder '$($script:PackageDir)' does not exist in the stage; nothing could be scanned." }
  $top = @(Get-ChildItem -Path $Stage -Force | ForEach-Object { $_.Name })
  if ($top.Count -ne 1 -or $top[0] -ne $script:PackageDir) { throw "RELEASE BUILD STOPPED: the stage must contain only the folder '$($script:PackageDir)', but contains: $($top -join ' ')" }
  $all = Get-ChildItem -Path $pkg -Recurse -Force
  $rel = { param($item) $item.FullName.Substring($Stage.Length + 1).Replace('\', '/') }
  $paths = $all | ForEach-Object { & $rel $_ }

  # Text files: everything outside bin/, plus bin/appsettings.json and bin/wwwroot/** (our own files).
  $textFiles = $all | Where-Object { -not $_.PSIsContainer } | Where-Object {
    $r = & $rel $_
    ($r -notlike "$($script:PackageDir)/bin/*") -or ($r -like "$($script:PackageDir)/bin/wwwroot/*") -or ($r -eq "$($script:PackageDir)/bin/appsettings.json")
  }

  # Guard: the scan must have something to look at.
  if (@($paths).Count -eq 0) { throw "RELEASE BUILD STOPPED: the safety scan found no paths in '$($script:PackageDir)'." }
  $textRel = @($textFiles | ForEach-Object { & $rel $_ })
  if ($textRel -notcontains "$($script:PackageDir)/bin/wwwroot/index.html") { throw "RELEASE BUILD STOPPED: bin/wwwroot/index.html is not among the files to scan." }
  if ($textRel -notcontains "$($script:PackageDir)/LICENSE") { throw "RELEASE BUILD STOPPED: LICENSE is not among the files to scan." }

  $problems = 0
  $private = Join-Path $Root "scripts\release-scan-private.txt"
  $ruleLines = @(Get-Content $rules) + $(if (Test-Path $private) { Write-Host "   (also using the machine-local rules in scripts\release-scan-private.txt)"; Get-Content $private } else { @() })
  foreach ($line in $ruleLines) {
    if ($line -match '^\s*(#|$)') { continue }
    $kind = $line.Substring(0, $line.IndexOf(':'))
    $rx = $line.Substring($line.IndexOf(': ') + 2)

    if ($kind -eq "path") {
      foreach ($p in $paths) {
        if ($p -match "(?i)$rx") { Write-Host "  PATH  $p  (rule: path: $rx)"; $problems++ }
      }
    }
    elseif ($kind -eq "text") {
      foreach ($f in $textFiles) {
        if ([System.IO.File]::ReadAllBytes($f.FullName) -contains 0) { continue }   # binary file: skipped, like grep -I
        $n = 0
        foreach ($text in [System.IO.File]::ReadLines($f.FullName)) {
          $n++
          if ($text -match "(?i)$rx") { Write-Host "  TEXT  $(& $rel $f) line $n  (rule: text: $rx)"; $problems++; break }
        }
      }
    }
    else { throw "RELEASE BUILD STOPPED: unknown rule type in ${rules}: $line" }
  }

  if ($problems -gt 0) { throw "RELEASE BUILD STOPPED: the safety scan found $problems problem(s) above. Nothing was zipped." }
  Write-Host "   scan passed: $(@($paths).Count) paths, $(@($textFiles).Count) text files checked"
}
