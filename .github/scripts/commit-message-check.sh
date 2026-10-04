#!/usr/bin/env bash
# Fails when a commit subject breaks the convention in docs/contributing/git-workflow.mdx:
#   type(scope): summary (#issue)
# The type and scope lists below are the source the doc describes; change both together.
#
#   usage: commit-message-check.sh <message-file>
set -euo pipefail

types='feat|fix|docs|refactor|test|perf|chore|ci|build'
scopes='activities|agents|api|app-integration|client|data-generation|database|delivery|docker|events|identity|imports|infrastructure|links|mcp|organization|planning|ppm|products|scoring|settings|skills|status-workflows|strategic-management|work'

# The subject is the first line that is neither blank nor a comment git strips before committing.
subject="$(grep -v '^#' "$1" | sed '/^[[:space:]]*$/d' | head -n 1)"

case "$subject" in
    'Merge '*|'Revert "'*|'fixup! '*|'squash! '*|'amend! '*) exit 0 ;;
esac

fail() {
    echo "commit-msg: $1" >&2
    echo "  subject: $subject" >&2
    echo "  expected: type(scope): summary (#issue) -- see docs/contributing/git-workflow.mdx" >&2
    exit 1
}

if ! printf '%s' "$subject" | grep -qE "^($types)(\([a-z-]+\))?!?: [^ ]"; then
    fail "the subject must start with one of these types: ${types//|/, }"
fi

scope="$(printf '%s' "$subject" | sed -nE 's/^[a-z]+\(([a-z-]+)\).*/\1/p')"
if [ -n "$scope" ] && ! printf '%s' "$scope" | grep -qxE "$scopes"; then
    fail "'$scope' is not a scope. Use one of: ${scopes//|/, } -- or leave the scope out"
fi

# wc -m counts characters only under a UTF-8 locale; under C it counts bytes, and an em dash is three.
length="$(printf '%s' "$subject" | LC_ALL=C.UTF-8 wc -m | tr -d ' ')"
if [ "$length" -gt 72 ]; then
    fail "the subject is $length characters; keep it to 72, issue reference included"
fi

case "$subject" in
    *.) fail "the subject must not end with a full stop" ;;
esac
