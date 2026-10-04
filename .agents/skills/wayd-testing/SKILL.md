---
name: wayd-testing
description: Writes, reviews, and verifies tests in the Wayd repository — .NET handlers and domain models (xUnit, FluentAssertions, Moq.AutoMock, fake DbContexts, Testcontainers) and the React client (Jest, React Testing Library) — and proves each test is worth keeping with a mutation gate that shows it fails when the production code is wrong. Use when adding, reviewing, or strengthening tests; when asked whether tests are any good, would catch a bug, or are too weak or shallow; when a change lands without tests; or when choosing where to add tests next.
---

# Wayd Testing

Two jobs: **write tests that match this repo's conventions**, and **prove they have value** before calling the work done.

The governing question for every test:

> **Would this test fail if the function body were emptied, or if it returned a default?**

A "no" is a strong smell of weak assertions — most such tests are coverage theatre. It is a heuristic, not a validity test: an idempotence check, a guard asserting that nothing happened, or a regression test pinning a deliberate no-op can answer "no" honestly, because it asserts a real observable invariant (`SaveChangesCallCount.Should().Be(0)`, state unchanged, no event raised). Keep those; strengthen anything that answers "no" without such an assertion. Never delete a test solely because it fails this question.

## Reference files

The general conventions — project layout, stack, naming, Arrange/Act/Assert, cancellation tokens, fakers, the fake DbContext, the collection fixture, running tests, traits — are in [docs/contributing/testing.mdx](../../../docs/contributing/testing.mdx). Read it before writing tests in an area you have not worked in. This skill adds only what that page does not cover.

| File | Read it when |
|---|---|
| [dotnet.md](dotnet.md) | Writing or reviewing any .NET test — handler shape, assertion style, the unit/integration tripwire, and what a fake DbContext cannot see. |
| [react.md](react.md) | Writing or reviewing a Jest test in the React client, or running the mutation gate against one. |
| [mutation-gate.md](mutation-gate.md) | Before reporting test work complete, whenever the gate applies (below). |
| [where-to-test-next.md](where-to-test-next.md) | Asked where coverage is weakest or what to test next, rather than to test a specific change. |

## Workflow for each change

1. **Read the code under test** and list the behaviours a test should pin. When the request enumerates behaviours, each one gets a dedicated test.
2. **Find the canonical test file** for that code and extend it rather than creating a narrower one. Match its assertion style (see [dotnet.md](dotnet.md#assertion-style)).
3. **Write the tests** to the conventions in testing.mdx plus [dotnet.md](dotnet.md) or [react.md](react.md).
4. **Run the self-review checklist** below. Always, whatever the size of the change.
5. **Run the mutation gate** when it applies — see the summary below and [mutation-gate.md](mutation-gate.md).
6. **Confirm CI will run the tests** — see [Which CI half runs it](#which-ci-half-runs-it).
7. **Report**: the mutation results table, anything deliberately left untested and why, and any finding you could not verify by running.

## The mutation gate in brief

The mutation gate proves each new or changed test fails when the code it covers is wrong: apply one real edit to the production code, run the covering test, record killed or survived, revert just that edit.

- **Applies when** the change adds or alters an asserted behaviour, or the request enumerated behaviours to verify. Default scope: **one mutation per asserted behaviour**, taking the Return and Guard rows of the catalogue first.
- **Runs only against a `Category=Unit` project** (or a single Jest suite). Never against an integration project.
- **A survivor is a real gap**: strengthen the test and re-apply the same mutation until it is killed.
- **Never revert a whole file** — undo only the mutation; the working tree may hold the user's uncommitted work.
- **Under-claim**: never report a gap you did not verify by running, and lead with the kill count.

The full loop, the catalogue, the revert rules, and the results-table template are in [mutation-gate.md](mutation-gate.md).

## Self-review checklist

Every rule this skill enforces, in one place. Run it on every change.

**Value**

- [ ] Would emptying the function body, or returning a default, fail every new test (or does the test assert an explicit invariant instead)?
- [ ] Concrete values asserted — not `NotBeNull()` or a type check alone?
- [ ] Where the operation touches more than its return value, a secondary observable asserted — related state, an event raised, `SaveChangesCallCount`?
- [ ] No tautology — nothing asserts a value the test just wrote reads back from the same in-memory object or fake? (A real database or HTTP round-trip in an integration test is legitimate.)
- [ ] Incidental fixtures not degenerate — ordering tested with more than one element, paging with a real page size — unless the degenerate value *is* the boundary under test?
- [ ] Near-identical tests collapsed into a `[Theory]` with `[InlineData]`?
- [ ] Every enumerated scenario mapped to a test — the exact symbol named, the full range the wording implies ("widened *or* narrowed" is two cases), positional qualifiers honoured literally?
- [ ] No `try { … } catch { }` swallowing the failure, no `Thread.Sleep`, no `[Skip]` to get green?

**Wayd conventions**

- [ ] `SaveChangesCallCount` asserted on **both** paths — `Be(1)` on success, `Be(0)` when a guard rejects?
- [ ] Time comes from `TestingDateTimeProvider` over a `FakeClock`, never `DateTime.UtcNow`?
- [ ] `TestContext.Current.CancellationToken` on every cancellable call, `// Arrange` / `// Act` / `// Assert` on every test, one SUT per file?
- [ ] Fakers in the domain test project's `Data/` folder (or the documented shared homes), extended with per-property `With{Property}`?
- [ ] New test files use FluentAssertions; additions to an existing file follow that file's style?
- [ ] No new `Testcontainers*` reference in a project that did not already need Docker?
- [ ] Integration tests use `[Collection(SqlServerTestCollection.Name)]`, never `IClassFixture<T>`, and assert only on data they created?
- [ ] For a PPM mutating handler, the `.Include` chain checked by eye (the gate cannot see it — [dotnet.md](dotnet.md#what-a-fake-dbcontext-cannot-see))?

**Scope**

- [ ] Nothing added for code where a bug is impossible or harmless — pass-through properties, records, framework-owned behaviour, generated files (`*.g.cs`, `wayd-api.ts`, migrations, `Internal/Generated/`)?
- [ ] Code with expensive failure covered however simple it looks — money, scoring, dates, status transitions, guard clauses, anything that has broken before?
- [ ] Anything a reader would expect covered but you deliberately skipped is named in the report with the reason?

## Which CI half runs it

A test CI never discovers has no value. `.github/scripts/dotnet-test-projects.sh` splits the .NET suite into two halves on one signal — whether the project references a `Testcontainers*` package:

```bash
./.github/scripts/dotnet-test-projects.sh unit          # the unit half — no Docker
./.github/scripts/dotnet-test-projects.sh integration   # the integration half — needs Docker running
```

Confirm a new test project appears in the half you expect. The one project whose `Category` and half disagree is `Wayd.Integrations.AzureDevOps.IntegrationTests` — see [dotnet.md](dotnet.md#the-testcontainers-tripwire).

Tests run on Microsoft.Testing.Platform, where a module that runs zero tests fails with exit code 8. Filter within one project, never across the solution.
