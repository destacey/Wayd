#!/usr/bin/env bash
# Warns when the AGENTS.md files an agent loads for one directory -- the root file plus every nested one
# on the way down -- add up to more than Codex reads (project_doc_max_bytes, 32 KiB by default). Codex
# silently drops whatever is past the limit, so the end of the guidance would go missing without an error.
# Sizes are counted with CRLF endings, the larger of the two checkouts.
#
#   usage: agents-md-size-check.sh    (exit 1 when any path is over)
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

limit=32768
status=0

size() { python3 -c 'import sys; print(sum(len(open(f, "rb").read().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")) for f in sys.argv[1:]))' "$@" 2>/dev/null \
    || python -c 'import sys; print(sum(len(open(f, "rb").read().replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")) for f in sys.argv[1:]))' "$@"; }

# Skill folders carry their own AGENTS.md, which is part of the skill, not an instruction file.
for file in $(git ls-files '*AGENTS.md' | grep -v -E '(^|/)(\.agents|\.claude|skills|node_modules)/'); do
    chain=()
    dir="$(dirname "$file")"
    while :; do
        [ -f "$dir/AGENTS.md" ] && chain=("$dir/AGENTS.md" "${chain[@]}")
        [ "$dir" = "." ] && break
        dir="$(dirname "$dir")"
    done
    total="$(size "${chain[@]}")"
    if [ "$total" -gt "$limit" ]; then
        echo "AGENTS.md: ${chain[*]} total $total bytes, over the $limit Codex reads; move detail into docs and link it." >&2
        status=1
    fi
done

exit $status
