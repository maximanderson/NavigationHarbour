#!/usr/bin/env bash
# Sourced by build-standalone.sh. Reads and validates the build-time product identity (product.json at the repository
# root) with a real JSON parser (node, which the frontend build already requires). Needs: $root.
# Sets: PRODUCT_NAME (display name) and PRODUCT_TECHNICAL_NAME (executable, distribution folder, package name).
# Stops with a clear message when the file or a required property is missing or invalid.

load_product_identity() {
  local file="$root/product.json" out
  [ -f "$file" ] || { echo "PRODUCT IDENTITY ERROR: $file not found." >&2; exit 65; }
  command -v node >/dev/null || { echo "PRODUCT IDENTITY ERROR: node is required to read product.json." >&2; exit 65; }
  out="$(node -e '
    const fs = require("fs");
    let p;
    try { p = JSON.parse(fs.readFileSync(process.argv[1], "utf8")); }
    catch (e) { console.error("product.json is not valid JSON: " + e.message); process.exit(1); }
    for (const k of ["displayName", "technicalName", "tagline", "description"]) {
      if (typeof p[k] !== "string" || p[k].trim() === "") { console.error("product.json: " + k + " is missing or empty."); process.exit(1); }
    }
    if (!/^[A-Za-z][A-Za-z0-9]{0,39}$/.test(p.technicalName)) {
      console.error("product.json: technicalName must start with a letter and contain only letters and digits (maximum 40 characters)."); process.exit(1);
    }
    if (/[\r\n]/.test(p.displayName)) { console.error("product.json: displayName must be a single line."); process.exit(1); }
    process.stdout.write(p.technicalName + "\n" + p.displayName);
  ' "$file")" || { echo "PRODUCT IDENTITY ERROR (see above)." >&2; exit 65; }
  PRODUCT_TECHNICAL_NAME="$(printf '%s' "$out" | sed -n 1p)"
  PRODUCT_NAME="$(printf '%s' "$out" | sed -n 2p)"
}
