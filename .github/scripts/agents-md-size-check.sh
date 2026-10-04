#!/usr/bin/env bash
# Warns when the AGENTS.md files an agent loads for one directory -- the root file plus every nested one
# on the way down -- add up to more than Codex reads (project_doc_max_bytes, 32 KiB by default). Codex
# silently drops whatever is past the limit, so the end of the guidance would go missing without an error.
# Sizes are counted with CRLF endings, the larger of the two checkouts.
#
#   usage: agents-md-size-check.sh [--staged]    (exit 1 when any path is over)
#   --staged measures what is about to be committed rather than the working tree.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

limit=32768
staged=0
[ "${1:-}" = --staged ] && staged=1

# Skill folders carry their own AGENTS.md, which is part of the skill, not an instruction file.
if [ "$staged" = 1 ]; then
    files="$(git ls-files --cached '*AGENTS.md')"
else
    files="$(git ls-files --cached --others --exclude-standard '*AGENTS.md')"
fi
files="$(printf '%s\n' "$files" | grep -v -E '(^|/)(\.agents|\.claude|skills|node_modules)/' || true)"

content() {
    if [ "$staged" = 1 ]; then git show ":$1"; else cat "$1"; fi
}

bytes() {
    # CRLF size: every line ending counts as two bytes.
    content "$1" | tr -d '\r' | LC_ALL=C awk '{ n += length($0) + 2 } END { print n + 0 }'
}

status=0
for file in $files; do
    total=0
    chain=""
    dir="$(dirname "$file")"
    while :; do
        candidate="$dir/AGENTS.md"
        [ "$dir" = "." ] && candidate="AGENTS.md"
        if printf '%s\n' "$files" | grep -qxF "$candidate"; then
            total=$((total + $(bytes "$candidate")))
            chain="$candidate $chain"
        fi
        [ "$dir" = "." ] && break
        dir="$(dirname "$dir")"
    done
    if [ "$total" -gt "$limit" ]; then
        echo "AGENTS.md: ${chain% } total $total bytes, over the $limit Codex reads; move detail into docs and link it." >&2
        status=1
    fi
done

exit $status
