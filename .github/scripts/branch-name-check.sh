#!/usr/bin/env bash
# Fails when a branch name breaks the convention in docs/contributing/git-workflow.mdx:
#   type/issue-short-summary   (the issue number is left out only when there is none)
# Bot branches are exempt: their names are chosen by the service that creates them.
#
#   usage: branch-name-check.sh <branch>
set -euo pipefail

branch="$1"
types='feat|fix|docs|refactor|test|perf|chore|ci|build'

case "$branch" in
    main|changeset-release/*|copilot/*) exit 0 ;;
esac

if ! printf '%s' "$branch" | grep -qxE "($types)/([0-9]+-)?[a-z0-9]+(-[a-z0-9]+)*"; then
    echo "branch '$branch' should be <type>/<issue>-<short-summary>, e.g. feat/964-mcp-tool-annotations." >&2
    echo "  types: ${types//|/, }; lowercase words joined by hyphens -- see docs/contributing/git-workflow.mdx" >&2
    exit 1
fi

if [ "${#branch}" -gt 60 ]; then
    echo "branch '$branch' is ${#branch} characters; keep it to 60." >&2
    exit 1
fi
