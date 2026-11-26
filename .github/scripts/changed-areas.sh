#!/usr/bin/env bash
# Decides which halves of the docker workflow a pull request must run, from the paths it changes, read one per line
# on stdin. Writes `dotnet=true|false` and `client=true|false` to stdout, in the form $GITHUB_OUTPUT takes, and on
# stderr the first path that turned each half on.
#
#   git -c core.quotePath=false diff --name-only --no-renames <base> <head> | bash .github/scripts/changed-areas.sh
#
# core.quotePath=false keeps a name with non-ASCII characters as it is: quoted, it starts with `"` and matches no
# pattern below, so a client file would run .NET alone.
#
# A path runs the .NET half unless it is listed below as one the .NET build and tests never read, so a path nobody
# thought about runs the tests rather than skipping them. The client half runs only for what the client reads: its
# own folder, docs/ (the in-app docs and their search index), specification.json (its generated API client) and the
# top-level README.md, CLAUDE.md and AGENTS.md (docs-path-traversal.test.ts probes for them one level above docs/).
#
# The halves read some of each other's files, which is why docs are not all skippable for .NET:
#   - Wayd.Web.Api embeds skills/**/*.md, and McpToolCatalogTests reads docs/getting-started/mcp-server.mdx and
#     evals/scenarios/.
#   - RecipeLibraryTests reads docs-site/static/schemas/.
# A new test that reads another file outside the .NET projects must add it to the first case below, which runs both
# halves, or to a .NET case.
set -euo pipefail

dotnet=false
client=false

turn_on() {
    local half="$1" path="$2"
    if [[ "${!half}" == "false" ]]; then
        printf -v "$half" true
        echo "$half: $path" >&2
    fi
}

while IFS= read -r path; do
    [[ -n "$path" ]] || continue
    case "$path" in
        # The workflow and what it runs, and files both halves' tooling reads.
        .github/workflows/docker.yml | .github/actions/* | .github/scripts/changed-areas.sh | \
            .editorconfig | .gitattributes | \
            Wayd.Web/src/Wayd.Web.Api/wwwroot/api/v1/specification.json | \
            docs/getting-started/mcp-server.mdx)
            turn_on dotnet "$path"
            turn_on client "$path"
            ;;
        .github/scripts/dotnet-test-projects.sh)
            turn_on dotnet "$path"
            ;;
        Wayd.Web/src/wayd.web.reactclient/* | README.md | CLAUDE.md | AGENTS.md)
            turn_on client "$path"
            ;;
        docs/*)
            turn_on client "$path"
            ;;
        docs-site/static/schemas/*)
            turn_on dotnet "$path"
            ;;
        # Read by no build or test: the docs site outside its schemas, agent guidance, other workflows and repository
        # tooling, the local git hooks and the infrastructure Terraform applies after the tests.
        docs-site/* | .agents/* | */AGENTS.md | */CLAUDE.md | \
            .github/* | .githooks/* | .iac/*)
            ;;
        # Other top-level Markdown; a deeper one, such as a skill, falls through to .NET.
        *.md)
            [[ "$path" == */* ]] && turn_on dotnet "$path"
            ;;
        *)
            turn_on dotnet "$path"
            ;;
    esac
done

echo "dotnet=$dotnet"
echo "client=$client"
