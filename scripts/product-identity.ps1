# Dot-sourced by build-standalone.ps1 and release-package.ps1. Reads and validates the build-time product identity
# (product.json at the repository root). Returns an object with Name (display name) and TechnicalName (executable,
# distribution folder, package name). Stops with a clear message when the file or a property is missing or invalid.

function Get-ProductIdentity([string]$Root) {
  $file = Join-Path $Root "product.json"
  if (-not (Test-Path $file)) { throw "PRODUCT IDENTITY ERROR: $file not found." }
  try { $p = Get-Content -Raw -Path $file | ConvertFrom-Json } catch { throw "PRODUCT IDENTITY ERROR: product.json is not valid JSON: $($_.Exception.Message)" }
  foreach ($k in "displayName", "technicalName", "tagline", "description") {
    $v = $p.$k
    if ($v -isnot [string] -or [string]::IsNullOrWhiteSpace($v)) { throw "PRODUCT IDENTITY ERROR: product.json: $k is missing or empty." }
  }
  if ($p.technicalName -cnotmatch '^[A-Za-z][A-Za-z0-9]{0,39}$') { throw "PRODUCT IDENTITY ERROR: product.json: technicalName must start with a letter and contain only letters and digits (maximum 40 characters)." }
  if ($p.displayName -match '[\r\n]') { throw "PRODUCT IDENTITY ERROR: product.json: displayName must be a single line." }
  [pscustomobject]@{ Name = $p.displayName; TechnicalName = $p.technicalName }
}
