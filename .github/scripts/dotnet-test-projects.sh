#!/usr/bin/env bash
# Runs one half of the .NET test suite.
#
#   unit         every test project that does NOT need Docker
#   integration  the Testcontainers suites (SQL Server), which need Docker
#
# Set COLLECT_COVERAGE=true to also collect Cobertura coverage (rules in testconfig.json). It is
# opt-in because instrumentation slows the run noticeably, and PRs do not publish a coverage report
# -- only the main-branch build does, where the extra time is not on anyone's feedback loop.
#
# The split is derived from Wayd.slnx at run time rather than hard-coded, so a new test project cannot
# be silently dropped from CI. A project is "integration" iff its .csproj has a PackageReference to a
# Testcontainers module -- the dependency that actually requires a Docker daemon, rather than a name or
# trait convention that can drift out of sync with what the project really needs. Directory.Build.targets
# stamps the Requires=Docker trait from the same signal, so a test's trait and the job it runs in agree.
# (Category is by project name, so a *.IntegrationTests project that needs no Docker runs in "unit".)
#
# This runs on the Linux CI runner AND on a developer's machine, where on Windows that means Git Bash.
# Keep the text processing to POSIX sed/tr: Git Bash's grep refuses `-P` outside a unibyte or UTF-8
# locale, and a matcher that silently finds nothing here selects no projects and tests nothing.
#
# The selection is handed to `dotnet test --solution` as a generated solution filter (.slnf). Two
# alternatives do not work here: `--project` takes only ONE project, and a solution-wide `--filter` fails
# every test module in which it matches nothing (Microsoft.Testing.Platform exit code 8, "zero tests ran").
set -euo pipefail

mode="${1:?usage: dotnet-test-projects.sh <unit|integration>}"
collect_coverage="${COLLECT_COVERAGE:-false}"
cd "$(dirname "$0")/../.."

# Extracted with sed rather than `grep -oP`: PCRE is a GNU extension, absent from BSD/macOS grep and
# refused by Git Bash's grep unless the locale is unibyte or UTF-8. On a Windows dev box it therefore
# matched nothing and the run selected no projects. Splitting on '<' first keeps one element per line,
# so the greedy `.*` cannot reach past the element it is matching.
mapfile -t all_projects < <(tr '<' '\n' < Wayd.slnx | sed -n 's/^Project .*Path="\([^"]*\)".*/\1/p')

selected=()
for proj in "${all_projects[@]}"; do
    # Only test projects are candidates; src projects have no tests to run.
    [[ "$proj" == *Tests.csproj ]] || continue
    # Matched on a PackageReference to any Testcontainers module, not the bare word: a comment or an
    # unrelated string mentioning Testcontainers must not move a project into the Docker job. Keep this
    # in step with the prefix match in Directory.Build.targets, which stamps the Requires=Docker trait.
    if grep -qE '<PackageReference[^>]*Include="Testcontainers' "$proj"; then
        [[ "$mode" == "integration" ]] && selected+=("$proj")
    else
        [[ "$mode" == "unit" ]] && selected+=("$proj")
    fi
done

if [[ ${#selected[@]} -eq 0 ]]; then
    echo "No $mode test projects found — the Wayd.slnx parse or the Testcontainers heuristic broke." >&2
    exit 1
fi

echo "Running ${#selected[@]} $mode test project(s):"
printf '  %s\n' "${selected[@]}"

# A solution filter needs Windows-style separators and its paths relative to the named solution.
filter="test-${mode}.slnf"
{
    printf '{\n  "solution": {\n    "path": "Wayd.slnx",\n    "projects": [\n'
    for i in "${!selected[@]}"; do
        sep=","
        [[ $i -eq $((${#selected[@]} - 1)) ]] && sep=""
        # JSON needs each Windows separator escaped, so "a/b" is written as "a\b".
        printf '      "%s"%s\n' "$(printf '%s' "${selected[$i]}" | sed 's#/#\\\\#g')" "$sep"
    done
    printf '    ]\n  }\n}\n'
} > "$filter"

parallel_args=()
if [[ "$mode" == "integration" ]]; then
    # Pull the image once, up front. Otherwise every suite that starts at the same time downloads it at the
    # same time, and the download eats into each container's start-up time.
    # sed, not `grep -oP` -- see the note on the project list above. Matched against the whole const
    # declaration so an unrelated `Name = "..."` appearing later cannot be picked up instead.
    image="$(sed -n 's/.*const string Name = "\([^"]*\)".*/\1/p' Wayd.Common/tests/Wayd.Tests.Containers/SqlServerTestImage.cs)"
    if [[ -z "$image" ]]; then
        echo "Could not read the SQL Server image from SqlServerTestImage.cs — the const was renamed or moved." >&2
        exit 1
    fi
    echo "Pulling $image"
    docker pull --quiet "$image"

    # Each integration project starts its own SQL Server container, and dotnet test runs every test module
    # at once by default. On a runner with far fewer cores than projects, every engine then warms up
    # together and early queries time out. Capping the modules caps the containers starting together.
    parallel_args=(--max-parallel-test-modules "${INTEGRATION_TEST_PARALLELISM:-2}")
    echo "Running at most ${INTEGRATION_TEST_PARALLELISM:-2} integration project(s) at a time"
fi

coverage_args=()
if [[ "$collect_coverage" == "true" ]]; then
    # Every module writes TestResults/coverage.cobertura.<timestamp>.xml at the repo root; the workflow
    # uploads that directory after both halves and merges once, so the published number spans unit AND
    # integration runs.
    coverage_args=(--coverlet)
    echo "Collecting coverage (testconfig.json)"
fi

# --hangdump aborts with a dump, and lists the tests still running, instead of burning the job's full
# time budget, which is how a container that never becomes ready would otherwise present.
dotnet test --solution "$filter" \
    -c Release \
    --results-directory TestResults \
    --hangdump --hangdump-timeout 5m \
    "${parallel_args[@]}" \
    "${coverage_args[@]}"
