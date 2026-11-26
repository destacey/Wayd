#!/usr/bin/env bash
# Runs one half of the .NET test suite.
#
#   unit         every test project that does NOT need Docker
#   integration  the Testcontainers suites (SQL Server), which need Docker; they share one engine this
#                script starts, unless WAYD_TEST_SQLSERVER already names one
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
    # One SQL Server for the whole run, shared by every suite through WAYD_TEST_SQLSERVER; each suite creates a
    # database of its own on it (SqlServerTestDatabase). The image ships system databases older than its
    # binaries, so every first boot spends about a minute upgrading them -- paid once here instead of once per
    # suite. A developer who already runs an engine can point WAYD_TEST_SQLSERVER at it and none is started.
    if [[ -z "${WAYD_TEST_SQLSERVER:-}" ]]; then
        # sed, not `grep -oP` -- see the note on the project list above. Matched against the whole const
        # declaration so an unrelated `Name = "..."` appearing later cannot be picked up instead.
        image="$(sed -n 's/.*const string Name = "\([^"]*\)".*/\1/p' Wayd.Common/tests/Wayd.Tests.Containers/SqlServerTestImage.cs)"
        if [[ -z "$image" ]]; then
            echo "Could not read the SQL Server image from SqlServerTestImage.cs — the const was renamed or moved." >&2
            exit 1
        fi
        echo "Pulling $image"
        docker pull --quiet "$image"

        container="wayd-test-sqlserver-$$"
        # Random per run, and only ever on loopback. Meets SQL Server's complexity rule (upper, lower, digit, symbol).
        password="Wayd-$(od -An -N12 -tx1 /dev/urandom | tr -d ' \n')-T1"
        startup_timeout="${SQLSERVER_STARTUP_TIMEOUT_SECONDS:-240}"

        remove_container() {
            docker rm --force --volumes "$container" > /dev/null 2>&1 || true
        }
        print_log() {
            echo "SQL Server log (tail):" >&2
            docker logs --tail 80 "$container" >&2 2>&1 || true
        }
        trap remove_container EXIT

        # Git Bash rewrites an argument that looks like a POSIX path into a Windows one, which would send docker
        # exec a sqlcmd path that does not exist in the container.
        export MSYS_NO_PATHCONV=1

        # The engine has crashed during start-up in CI, dumping core before accepting a login. Nothing has been
        # written to it then, so a fresh container is started in its place.
        ready=false
        outcome=""
        for attempt in 1 2 3; do
            echo "Starting SQL Server ($container, attempt $attempt)"
            docker run --detach --name "$container" \
                --env ACCEPT_EULA=Y \
                --env MSSQL_SA_PASSWORD="$password" \
                --env MSSQL_MEMORY_LIMIT_MB="${SQLSERVER_MEMORY_LIMIT_MB:-6144}" \
                --publish 127.0.0.1::1433 \
                "$image" > /dev/null

            # Waits on a real sa login, not the port or the "ready for client connections" line: the engine
            # prints that while it is still upgrading, and a login then fails with "An error occurred while
            # evaluating the password".
            deadline=$((SECONDS + startup_timeout))
            outcome="timed out"
            while (( SECONDS < deadline )); do
                # Only a definite "false" is a crash; a failed inspect is retried on the next pass rather than
                # taken for one.
                if [[ "$(docker inspect --format '{{.State.Running}}' "$container" 2>/dev/null || true)" == "false" ]]; then
                    outcome="stopped"
                    break
                fi
                if docker exec "$container" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$password" \
                    -Q "SET NOCOUNT ON; SELECT 1;" > /dev/null 2>&1; then
                    ready=true
                    break
                fi
                sleep 2
            done

            [[ "$ready" == "true" ]] && break
            print_log
            remove_container
            # A slow engine is not a crashed one; only a container that stopped is worth starting again.
            [[ "$outcome" == "stopped" ]] || break
        done

        if [[ "$ready" != "true" ]]; then
            if [[ "$outcome" == "stopped" ]]; then
                echo "SQL Server stopped during start-up on all three attempts; its log is above." >&2
            else
                echo "SQL Server did not accept an sa login within ${startup_timeout}s; its log is above." >&2
            fi
            exit 1
        fi

        # `docker port` prints one line per address family; the loopback publish gives exactly one.
        port="$(docker port "$container" 1433/tcp | head -n 1 | sed 's/.*://')"
        if [[ ! "$port" =~ ^[0-9]+$ ]]; then
            echo "Could not read the port Docker published for SQL Server (got '$port')." >&2
            exit 1
        fi
        export WAYD_TEST_SQLSERVER="Server=127.0.0.1,$port;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=True"
        echo "SQL Server ready on 127.0.0.1:$port after ${SECONDS}s of the run"
    else
        echo "Using the SQL Server in WAYD_TEST_SQLSERVER"
    fi

    # With no engines booting beside each other, the cap is about the test processes themselves sharing the
    # runner's cores and the one engine.
    parallel_args=(--max-parallel-test-modules "${INTEGRATION_TEST_PARALLELISM:-4}")
    echo "Running at most ${INTEGRATION_TEST_PARALLELISM:-4} integration project(s) at a time"
fi

coverage_args=()
if [[ "$collect_coverage" == "true" ]]; then
    # Every module writes TestResults/coverage.cobertura.<timestamp>.xml at the repo root; the workflow
    # uploads that directory after both halves and merges once, so the published number spans unit AND
    # integration runs.
    #
    # The unit half clears the previous run's files first -- the names are unique per module AND per run,
    # so on a developer's machine they accumulate, and a merge over the directory would then count a stale
    # copy of a module beside its current one. Only here: the integration half must add to what the unit
    # half just wrote. CI starts from an empty checkout, so this is a no-op there.
    if [[ "$mode" == "unit" ]]; then
        rm -f TestResults/coverage.cobertura.*.xml
    fi
    coverage_args=(--coverlet)
    echo "Collecting coverage (testconfig.json)"
fi

# --hangdump aborts with a dump, and lists the tests still running, instead of burning the job's full
# time budget, which is how a container that never becomes ready would otherwise present.
status=0
dotnet test --solution "$filter" \
    -c Release \
    --results-directory TestResults \
    --hangdump --hangdump-timeout 5m \
    "${parallel_args[@]}" \
    "${coverage_args[@]}" || status=$?

# A failure that came from the engine shows up in its log, not the tests' output.
if [[ $status -ne 0 && -n "${container:-}" ]]; then
    print_log
fi
exit $status
