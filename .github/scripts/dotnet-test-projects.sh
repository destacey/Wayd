#!/usr/bin/env bash
# Runs one half of the .NET test suite.
#
#   unit         every test project that does NOT need Docker
#   integration  the Testcontainers suites (SQL Server), which need Docker
#
# Set COLLECT_COVERAGE=true to also collect Cobertura coverage (see coverage.runsettings). It is
# opt-in because instrumentation slows the run noticeably, and PRs do not publish a coverage report
# -- only the main-branch build does, where the extra time is not on anyone's feedback loop.
#
# The split is derived from Wayd.slnx at run time rather than hard-coded, so a new test project cannot
# be silently dropped from CI. A project is "integration" iff its .csproj has a PackageReference to a
# Testcontainers module -- the dependency that actually requires a Docker daemon, rather than a name or
# trait convention that can drift out of sync with what the project really needs. Directory.Build.targets
# derives the Category trait from the same signal, so a test's trait and the job it runs in agree.
#
# The selection is handed to `dotnet test` as a generated solution filter (.slnf). Two alternatives do
# not work here: `dotnet test` takes only ONE project argument (MSB1008 on a list), and a solution-wide
# `--filter` exits non-zero under xUnit v3 for every assembly that contains no matching test.
set -euo pipefail

mode="${1:?usage: dotnet-test-projects.sh <unit|integration>}"
collect_coverage="${COLLECT_COVERAGE:-false}"
cd "$(dirname "$0")/../.."

mapfile -t all_projects < <(grep -oP '(?<=<Project Path=")[^"]+' Wayd.slnx)

selected=()
for proj in "${all_projects[@]}"; do
    # Only test projects are candidates; src projects have no tests to run.
    [[ "$proj" == *Tests.csproj ]] || continue
    # Matched on a PackageReference to any Testcontainers module, not the bare word: a comment or an
    # unrelated string mentioning Testcontainers must not move a project into the Docker job. Keep this
    # in step with the prefix match in Directory.Build.targets, which derives the Category trait.
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

coverage_args=()
if [[ "$collect_coverage" == "true" ]]; then
    # Results land in each project's TestResults/<guid>/coverage.cobertura.xml; the workflow collects
    # them from both jobs and merges once, so the published number spans unit AND integration runs.
    coverage_args=(--collect:"XPlat Code Coverage" --settings coverage.runsettings)
    echo "Collecting coverage (coverage.runsettings)"
fi

# --blame-hang-timeout aborts with a dump naming the stuck test instead of burning the job's full
# time budget, which is how a container that never becomes ready would otherwise present.
dotnet test "$filter" \
    -c Release \
    --verbosity normal \
    --blame-hang-timeout 5m \
    "${coverage_args[@]}"
