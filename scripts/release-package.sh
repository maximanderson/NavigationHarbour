#!/usr/bin/env bash
# Sourced by build-standalone.sh when --release is used. Needs: $root $project $out $version.
#
# Builds artifacts/release/PortalCore-win-x64-v<version>.zip (+ .sha256) containing ONE top-level PortalCore/ folder.
#
# Safety, in two layers:
#  1. ALLOWLIST: only the files named in this script are copied into the package. Nothing is copied by directory
#     from the development tree (no frontend/public, no backend Data/, no docs/ glob).
#  2. SCAN: afterwards every path and every text file is checked against scripts/release-scan-rules.txt. A hit stops
#     the build and no ZIP is written. The scan is an extra safeguard, not a replacement for the allowlist.

# Documentation that ships in the package (explicit list, in docs/).
RELEASE_DOCS=(release-package windows-deployment server-configuration standalone-hosting reverse-proxy backup-restore logging)

release_fail() { echo "RELEASE BUILD STOPPED: $*" >&2; exit 1; }

release_scan() {
  local stage="$1" rules="$root/scripts/release-scan-rules.txt" private="$root/scripts/release-scan-private.txt" problems=0 kind rx file line n
  [ -f "$rules" ] || release_fail "scan rules not found: $rules"
  [ -f "$private" ] && echo "   (also using the machine-local rules in scripts/release-scan-private.txt)"

  # Package-relative paths, '/'-separated.
  local paths; paths="$(cd "$stage" && find PortalCore -mindepth 1 \( -type f -o -type d \) | sort)"

  # Text files to scan: everything outside bin/, plus bin/appsettings.json and bin/wwwroot/** (our own files).
  # The runtime and framework DLLs in bin/ are binary and not ours.
  local textfiles; textfiles="$(cd "$stage" && find PortalCore -type f \( -path 'PortalCore/bin/wwwroot/*' -o -path 'PortalCore/bin/appsettings.json' -o -not -path 'PortalCore/bin/*' \) | sort)"

  while IFS= read -r rule; do
    case "$rule" in ''|'#'*) continue ;; esac
    kind="${rule%%:*}"; rx="${rule#*: }"
    if [ "$kind" = "path" ]; then
      while IFS= read -r file; do
        [ -z "$file" ] && continue
        printf '%s\n' "$file" | grep -P -i -q -- "$rx" && { echo "  PATH  $file  (rule: path: $rx)" >&2; problems=$((problems + 1)); }
      done <<< "$paths"
    elif [ "$kind" = "text" ]; then
      while IFS= read -r file; do
        [ -z "$file" ] && continue
        # grep -P for look-aheads; -I skips binary files; only line numbers are reported, never the matched text.
        # "no match" is the normal case; under set -e/pipefail it must not stop the script.
        n="$( (cd "$stage" && grep -P -i -I -n -o -- "$rx" "$file" 2>/dev/null | cut -d: -f1 | sort -un | head -3 | tr '\n' ',') || true )"
        [ -n "$n" ] && { echo "  TEXT  $file line(s) ${n%,}  (rule: text: $rx)" >&2; problems=$((problems + 1)); }
      done <<< "$textfiles"
    else
      release_fail "unknown rule type in $rules: $rule"
    fi
  done < <(cat "$rules" "$private" 2>/dev/null)

  [ "$problems" -eq 0 ] || release_fail "the safety scan found $problems problem(s) above. Nothing was zipped. Fix or remove the files, then run again."
  echo "   scan passed: $(printf '%s\n' "$paths" | grep -c .) paths, $(printf '%s\n' "$textfiles" | grep -c .) text files checked"
}

stage_release() {
  local stage="$1" pkg="$1/PortalCore" d f
  rm -rf "$stage"; mkdir -p "$pkg"

  echo "== Frontend build"
  ( cd "$root/frontend" && { [ -d node_modules ] || npm ci; } && npm run build )

  echo "== Backend publish (win-x64, self-contained, no .pdb)"
  dotnet publish "$project/PortalCore.Api.csproj" -c Release -r win-x64 --self-contained true \
    -p:DebugType=None -p:DebugSymbols=false -o "$pkg/bin"

  echo "== Frontend -> bin/wwwroot (allowlist: index.html, assets/, favicon.svg, icons.svg)"
  mkdir -p "$pkg/bin/wwwroot"
  cp "$root/frontend/dist/index.html" "$pkg/bin/wwwroot/"
  cp -R "$root/frontend/dist/assets" "$pkg/bin/wwwroot/assets"
  for f in favicon.svg icons.svg; do cp "$root/frontend/dist/$f" "$pkg/bin/wwwroot/$f"; done

  echo "== Directories"
  for d in conf data logs backups certs ai docs public; do mkdir -p "$pkg/$d"; done
  # Empty directories survive a ZIP only with a file in them.
  for d in data logs backups certs; do printf 'Portal Core creates and uses files here. Keep this folder.\r\n' > "$pkg/$d/README.txt"; done
  cp "$project/conf/server.sample.ini" "$pkg/conf/server.sample.ini"

  echo "== Demonstration data (allowlist)"
  mkdir -p "$pkg/public/config" "$pkg/public/navigation" "$pkg/public/content" "$pkg/ai/knowledge/csv" "$pkg/ai/prompts/csv"
  cp "$root/demo/config/portal.ini"                 "$pkg/public/config/portal.ini"
  cp "$root/demo/navigation/demo-navigation.json"   "$pkg/public/navigation/demo-navigation.json"
  cp "$root/demo/navigation/demo-branches.json"     "$pkg/public/navigation/demo-branches.json"
  cp "$root/demo/content/reports.html"              "$pkg/public/content/reports.html"
  cp "$root/demo/ai/knowledge/csv/cities.csv"       "$pkg/ai/knowledge/csv/cities.csv"
  cp "$root/demo/ai/prompts/csv/contextPrompt.txt"  "$pkg/ai/prompts/csv/contextPrompt.txt"

  echo "== Documentation (allowlist)"
  for d in "${RELEASE_DOCS[@]}"; do cp "$root/docs/$d.md" "$pkg/docs/$d.md"; done
  cp "$root/deploy/START-HERE.txt" "$pkg/START-HERE.txt"
  printf 'Portal Core %s (win-x64, self-contained)\r\n' "$version" > "$pkg/VERSION.txt"
  cp "$root/LICENSE" "$root/THIRD-PARTY-NOTICES.md" "$pkg/"
  mkdir -p "$pkg/bin/licenses" && cp "$root"/deploy/licenses/* "$pkg/bin/licenses/"   # full third-party licence texts
}


build_release() {
  local stage="$out/stage" name="PortalCore-win-x64-v$version" zip
  zip="$out/$name.zip"
  rm -f "$zip" "$zip.sha256"

  stage_release "$stage"

  echo "== Safety scan (rules: scripts/release-scan-rules.txt)"
  release_scan "$stage"

  echo "== ZIP -> $zip"
  ( cd "$stage" && zip -q -r -X "$zip" PortalCore )
  ( cd "$out" && sha256sum "$name.zip" > "$name.zip.sha256" )

  rm -rf "$stage"
  echo "Done."
  echo "  $zip ($(du -h "$zip" | cut -f1))"
  echo "  $(cat "$zip.sha256")"
}
