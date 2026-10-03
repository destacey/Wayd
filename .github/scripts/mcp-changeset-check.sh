#!/usr/bin/env bash
# Fails when a branch changes what @wayd/mcp publishes without adding a changeset.
#
# A change with no changeset merges cleanly and is never released: the release PR is opened only for
# pending changesets, so the published package silently falls behind the API it calls. A change that
# should not be released still needs one -- `npx changeset --empty` records that decision.
#
# What ships is src/, README.md and package.json, plus what scripts/ generates into src/ at build time.
# The import formats are generated from the API's OpenAPI document, so a spec change counts only when
# it changes the generated output; most API changes touch no import endpoint and need nothing here.
#
#   usage: mcp-changeset-check.sh [base-ref]    (default origin/main)
set -euo pipefail

base_ref="${1:-origin/main}"
cd "$(dirname "$0")/../.."

mcp="Wayd.Web/src/Wayd.Mcp"
spec="Wayd.Web/src/Wayd.Web.Api/wwwroot/api/v1/specification.json"
base="$(git merge-base "$base_ref" HEAD)"

if git diff --name-only --diff-filter=A "$base" HEAD -- "$mcp/.changeset/*.md" \
    | grep -qvx "$mcp/.changeset/README.md"; then
  echo "Changeset found."
  exit 0
fi

shipped="$(git diff --name-only "$base" HEAD -- "$mcp/src" "$mcp/scripts" "$mcp/README.md" "$mcp/package.json")"

if [ -z "$shipped" ] && ! git diff --quiet "$base" HEAD -- "$spec"; then
  generated="$mcp/src/generated/import-formats.ts"
  work="$(mktemp -d)"
  cp "$spec" "$work/spec.json"
  trap 'cp "$work/spec.json" "$spec"; rm -rf "$work"' EXIT

  (cd "$mcp" && npx tsx scripts/generate-import-formats.ts > /dev/null)
  cp "$generated" "$work/head.ts"
  git show "$base:$spec" > "$spec"
  (cd "$mcp" && npx tsx scripts/generate-import-formats.ts > /dev/null)

  if ! cmp -s "$generated" "$work/head.ts"; then
    shipped="$spec (changes the generated import formats)"
  fi
  cp "$work/head.ts" "$generated"
fi

if [ -n "$shipped" ]; then
  echo "::error::These changes reach the published @wayd/mcp package but no changeset was added:"
  echo "$shipped"
  echo "Run 'npx changeset' in $mcp, or 'npx changeset --empty' if this should not be released."
  exit 1
fi

echo "Nothing that ships changed."
