# Where to test next

For "of all the untested code, what should be tackled first?" — a periodic sweep, not a per-change check.

## Getting a ranking

Rank .NET methods by CRAP score (complexity combined with coverage). It needs Cobertura coverage, which PRs deliberately do not collect:

- **From CI** — every push to `main` merges both halves' coverage and uploads a `coverage-report` artifact (kept 14 days) from `.github/workflows/docker.yml`. Its `html/` folder is a ReportGenerator report; use its risk-hotspot view rather than paying for coverage locally.
- **Locally** — `COLLECT_COVERAGE=true ./.github/scripts/dotnet-test-projects.sh unit` (then `integration`) writes `TestResults/coverage.cobertura.*.xml` at the repo root; run ReportGenerator over those files.

The ranking covers .NET only. The client's Jest coverage is reported separately, and the MCP server needs its own judgement.

## Using it

- **A worklist, not a gate.** A high score means "look here", not "add tests until the number falls".
- **It cannot say whether a test is necessary.** It measures the shape of the code, not the cost of a bug — apply the Scope items of the self-review checklist.
- Once it points at a method, write tests to the conventions and run the mutation gate as for any change.
