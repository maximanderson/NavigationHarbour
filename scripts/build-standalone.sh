#!/usr/bin/env bash
# Builds the product (name from product.json) for standalone hosting (one Kestrel process serves the frontend, the API and the public files).
#
# Usage: scripts/build-standalone.sh [--windows] [--release --version X.Y.Z] [output-directory]
#
#   (no option)  framework-dependent publish for the machine running this script (as before)
#   --windows    Windows x64, self-contained: no IIS and no installed .NET runtime needed on the server.
#                Not single-file, not trimmed. Can be built on Linux or Windows.
#   --release    (with --windows and --version) the shareable package: demonstration data only, an explicit
#                file allowlist, a safety scan, and artifacts/release/<TechnicalName>-win-x64-vX.Y.Z.zip + .sha256.
#
# Result (the product home):
#   bin/       published backend (<TechnicalName>.exe with --windows), the React build in bin/wwwroot
#   conf/      server.sample.ini only; server.ini is private, created by the administrator, never overwritten
#   public/    config/ (standalone portal.ini), navigation/, content/ - NEW installation only, never overwritten
#   data/ logs/ backups/ certs/ ai/ docs/     empty directories (ai/ gets the sample knowledge and prompts once)
#
# The frontend uses the existing Vite build (npm run build). config/, navigation/ and content/ are removed from
# wwwroot on purpose: they are served from <out>/public, so they can be edited without a rebuild.
set -euo pipefail

windows=false
release=false
version=""
out_arg=""
while [ $# -gt 0 ]; do
  case "$1" in
    --windows) windows=true ;;
    --release) release=true ;;
    --version) shift; version="${1:-}" ;;
    -*) echo "Unknown option: $1" >&2; exit 64 ;;
    *) out_arg="$1" ;;
  esac
  shift
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$root/backend/Portal.Api"

source "$root/scripts/product-identity.sh"
load_product_identity

# --release: a versioned, shareable package with DEMONSTRATION data only (see docs/release-package.md).
# It never copies the development navigation, configuration or AI files. Handled completely by the block below.
if $release; then
  $windows || { echo "--release requires --windows (the release package is Windows x64 self-contained)." >&2; exit 64; }
  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]] || { echo "--release requires --version <major.minor.patch>, for example --version 1.0.0" >&2; exit 64; }
  out="$(mkdir -p "${out_arg:-$root/artifacts/release}" && cd "${out_arg:-$root/artifacts/release}" && pwd)"
  source "$root/scripts/release-package.sh"
  build_release
  exit 0
fi

out="$(mkdir -p "${out_arg:-$root/artifacts/$PRODUCT_TECHNICAL_NAME}" && cd "${out_arg:-$root/artifacts/$PRODUCT_TECHNICAL_NAME}" && pwd)"

echo "== Frontend build"
cd "$root/frontend"
[ -d node_modules ] || npm ci
npm run build

echo "== Backend publish -> $out/bin"
if $windows; then
  dotnet publish "$project/Portal.Api.csproj" -c Release -r win-x64 --self-contained true -o "$out/bin"
else
  dotnet publish "$project/Portal.Api.csproj" -c Release -o "$out/bin"
fi

# The package must never contain a database.
if find "$out/bin" \( -iname '*.db' -o -iname '*.db-wal' -o -iname '*.db-shm' \) | grep -q .; then
  echo "ERROR: a SQLite database file was found in $out/bin; refusing to continue." >&2
  exit 1
fi

echo "== Frontend -> $out/bin/wwwroot"
rm -rf "$out/bin/wwwroot"
mkdir -p "$out/bin/wwwroot"
cp -R dist/. "$out/bin/wwwroot/"
for folder in config navigation content; do
  rm -rf "$out/bin/wwwroot/$folder"
done

echo "== Directories"
for folder in conf data logs backups certs ai docs; do
  mkdir -p "$out/$folder"
done
cp "$project/conf/server.sample.ini" "$out/conf/server.sample.ini"   # never server.ini

if [ ! -d "$out/public" ]; then
  echo "== Initial public files -> $out/public"
  mkdir -p "$out/public"
  for folder in navigation content; do
    [ -d "public/$folder" ] && cp -R "public/$folder" "$out/public/$folder"
  done
  mkdir -p "$out/public/config"
  cp "$root/deploy/portal.standalone.ini" "$out/public/config/portal.ini"   # same-origin API settings
else
  echo "== $out/public exists; left unchanged"
fi

if [ -z "$(ls -A "$out/ai")" ]; then
  echo "== Sample AI knowledge and prompts -> $out/ai"
  cp -R "$project/Data/knowledge" "$out/ai/knowledge"
  cp -R "$project/Data/prompts" "$out/ai/prompts"
fi

echo "== Documentation -> $out/docs"
for doc in windows-deployment standalone-hosting server-configuration backup-restore logging; do
  [ -f "$root/docs/$doc.md" ] && cp "$root/docs/$doc.md" "$out/docs/$doc.md"
done

echo "Done: $out"
echo "Next: copy conf/server.sample.ini to conf/server.ini and edit it, then run: $PRODUCT_TECHNICAL_NAME --home <home> --init-db"
