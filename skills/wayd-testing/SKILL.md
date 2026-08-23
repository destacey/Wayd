---
name: wayd-testing
description: Writes and audits tests in the Wayd repository, and verifies they are worth keeping. Use when adding, reviewing, or strengthening tests for .NET handlers, domain models, or the React client — and whenever asked whether tests are any good, would catch a bug, are too weak or shallow, or when a change lands without tests. Covers Wayd's xUnit/FluentAssertions/Moq.AutoMock conventions, faker placement, the unit/integration split, and a mutation-verification gate that proves each test fails when the code is wrong.
---

# Wayd Testing

Two jobs: **write tests that match this repo's conventions**, and **prove they have value** before calling the work done.

The governing question for every test:

> **Would this test fail if the function body were emptied, or if it returned a default?**

Treat a "no" as a strong smell of weak assertions, and look again — most such tests are coverage theatre, making the number go up while catching nothing.

It is a heuristic, not a validity test. A few legitimate tests answer "no" honestly: an idempotence check, a guard asserting that nothing happened, a regression test pinning a deliberate no-op. What makes those valid is that they assert a real observable invariant — `SaveChangesCallCount.Should().Be(0)`, state unchanged, no event raised. Keep those; strengthen anything that answers "no" without such an assertion. Never delete a test solely because it fails this question.

---

## Part 1 — Conventions

The authoritative reference is [docs/contributing/testing.mdx](../../docs/contributing/testing.mdx). This section covers what is easy to get wrong; read the doc for the full picture.

### Stack

| Purpose | Library |
|---|---|
| Test framework | xUnit |
| Assertions | FluentAssertions (`.Should()...`) — never `Assert.Equal` |
| Mocking | Moq, with Moq.AutoMock for automatic dependency mocking |
| Fake data | Bogus, via the repo's fakers |
| Architecture rules | NetArchTest.Rules |

### Structure

- **One SUT per file; class name = file name.** Never merge several handlers into one test class. Each handler gets its own `{Handler}Tests.cs`.
- **`// Arrange` / `// Act` / `// Assert` on every test method.** Append a note to the marker when the setup needs explaining: `// Arrange — an active model cannot be deleted; the guard should reject it`.
- Test projects are named `{ProjectName}.Tests` and mirror the source structure.

### Cancellation tokens

Pass `TestContext.Current.CancellationToken` to **every** call that accepts a `CancellationToken` — the handler's `Handle(...)` **and** every EF assertion query (`SingleAsync`, `AnyAsync`, …).

Never `CancellationToken.None`. It trips the `xUnit1051` analyzer and breaks the dominant convention.

### Handler test shape

```csharp
public class ActivateScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly ScoringModelFaker _faker = new();

    private ActivateScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, NullLogger<ActivateScoringModelCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldTransitionProposedModelToActive()
    {
        // Arrange
        var model = _faker.AsProposedWsjf();
        _dbContext.ScoringModels.Add(model);
        var command = new ActivateScoringModelCommand(model.Id);

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.State.Should().Be(ScoringModelState.Active);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
```

Assert `SaveChangesCallCount` on **both** paths — `Be(1)` when the handler should persist, `Be(0)` when a guard should reject. A failure test that omits it passes even if the handler saved the bad state.

Handlers return `Result<T>`; assert on `IsSuccess`/`IsFailure`, not on thrown exceptions. Passing `result.Error` as the FluentAssertions `because` argument makes a failure self-diagnosing.

### Fakers

- Domain fakers live in **the domain's own test project**, under `Data/` — e.g. `Wayd.Common.Domain.Tests/Data/`. Default here; do not invent a new shared project for them.
- **Two intentional exceptions — reuse these, do not duplicate or relocate them:**
  - **Cross-cutting fakers** for types owned by no single domain live in `Wayd.Tests.Shared/Data/` — `FeatureFlagFaker`, `OidcProviderFaker`, `PersonalAccessTokenFaker`.
  - **Organization** publishes its fakers from a dedicated `Wayd.Organization.TestData` project (`TeamFaker`, `TeamMembershipFaker`, …), because several test projects consume them.
- Only the `PrivateConstructorFaker<T>` base lives centrally, in `Wayd.TestData.Core` (`Wayd.Common/tests/Wayd.TestData.Core/`, namespace `Wayd.TestData.Core`). Test projects reach it transitively via their `Wayd.Tests.Shared` reference — no extra project reference needed.
- **Per-property `With{Property}` extensions**, one `RuleFor` each, returning the faker for chaining. Not a single `WithData(...)` with optional parameters.
- **`With{Property}` means "set exactly this value"** — including null. `WithDescription(null)` must produce a null description, not skip the assignment.
- **`As...` helpers** set a coherent domain state across several aligned properties (`AsActive()` → status + date range + timestamps). When they take optional parameters, call the matching `With{Property}` only when a value is supplied.
- **Shared fixtures are faker extensions**, never a `{Thing}TestData` holder class. Keep one-off fixtures inline.
- Composition helpers should build children **through the aggregate's own methods**, so invariants and ordering hold.

### Unit vs integration — the classification tripwire

A project is **Integration** if and only if it references a `Testcontainers*` package. That single fact drives everything: `Directory.Build.targets` stamps the `Category` assembly trait from it, and `.github/scripts/dotnet-test-projects.sh` splits the two CI jobs on the same signal.

**Adding a `Testcontainers` reference to a unit project silently moves every test in it into the Docker-requiring CI job.** Never add one to make a single test work — put that test in the area's integration project instead.

Never hand-tag `Category`. It is derived, and a hand-tag can drift from what the project actually needs.

### Integration tests

- Use `[Collection(SqlServerTestCollection.Name)]`, **never `IClassFixture<T>`**. xUnit builds a class fixture once per test class, so `IClassFixture` starts a container and migrates a schema for every class — that mistake once cost six containers for 44 tests.
- Tests **share one database**. Scope every assertion to data the test itself created (a fresh `Guid`, a unique key). Never assert on "the only row" or an empty schema.

### PPM authorization — the blind spot mutation testing cannot see

A **human mutating an existing** project, program, or portfolio requires **delivery leadership** (Owner/Manager on the record or an ancestor), not just a permission claim. Handlers must load the record's own roles **and** its ancestors' in the query:

```csharp
.Include(p => p.Roles)                                 // the record's own leadership
.Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
.Include(p => p.Program).ThenInclude(p => p!.Roles)    // projects only
```

Omitting `.Include(p => p.Roles)` denies an Owner/Manager on the record itself. See `UpdateProjectCommand.cs` for the full chain (it also uses `.AsSplitQuery()`).

**Creation and import paths are deliberately exempt** — they pass `PpmActor.System` under the caller's Create/Import permission, because nobody can hold a role on a record that does not exist yet. Those commands are not `IRequireLinkedEmployee`, and no ancestry is expected. Do not add leadership assertions to them; `grep PpmActor.System` audits every bypass.

**On the paths where it does apply, a missing `.Include` silently empties the ancestry and denies a legitimately authorized user.** The compiler cannot catch it — every mutating aggregate method requires a `PpmActor`, but none require the ancestry to be populated.

**Neither can any test using a fake DbContext, because `.Include` is a no-op in memory.** Mutation analysis is blind here too: deleting the `.Include` changes no in-memory assertion, so the mutation "survives" for a reason that has nothing to do with assertion strength.

So this one is a **read-the-query check, not a test check**. When touching a PPM mutating handler, verify the `.Include` chain by eye, and cover the authorization rule itself in the aggregate's own domain tests (where roles are real objects) rather than trusting the handler test.

See [CLAUDE.md](../../CLAUDE.md) and [docs/contributing/architecture.mdx](../../docs/contributing/architecture.mdx#permission-based-vs-membership-based-authorization).

### Other repo rules that reach into tests

- **Never `DateTime.UtcNow`** — inject `IDateTimeProvider`. A test asserting on real wall-clock time is flaky by construction.
- NodaTime (`Instant`, `LocalDate`) for time values.
- No `Async` suffix on new async methods.

---

## Part 2 — Writing tests worth keeping

### Cover these categories

| Category | Examples |
|---|---|
| Happy path | Valid input produces the expected result |
| Edge cases | Empty, boundary, zero/negative, special characters |
| Error cases | Invalid input, nulls, guard rejections, timeouts |
| State transitions | Before/after, initialization, status changes |

### Assertion rules

- **Concrete values, not existence.** `.Should().NotBeNull()` alone is not an assertion — it passes when the method returns an empty object. Assert the value.
- **No tautologies.** Do not assert that a value you just wrote reads back unchanged **from the same in-memory object or fake** — that asserts the test's own setup. This does not forbid genuine round-trips: a real database or API round-trip in an integration test is legitimate behaviour under test, and catches mapping, conversion, and serialization bugs a fake never will.
- **Behaviour radius.** When an operation touches more than its return value, assert at least one secondary observable — related state, a neighbouring field, an event raised, `SaveChangesCallCount`.
- **Property intersections.** When code handles independent properties, add at least one test combining several at once. Bugs live at intersections, not on single axes.
- **Fixture realism.** Do not let a degenerate fixture hide the dimension you meant to exercise — ordering with one element, or paging with a page size of zero, proves nothing about ordering or paging. This applies to *incidental* setup only: when the degenerate value **is** the boundary or guard under test (page size 0 rejected, empty collection returns empty, negative quantity refused), it is exactly the right input.
- **Parameterize.** Prefer `[Theory]` with `[InlineData]` over near-identical `[Fact]`s. Never write several tests whose only difference is an input value.

### Scenario fidelity

When the request enumerates behaviours, map each to a dedicated test before reporting done.

- **Test the exact symbol named** — not a neighbouring helper that looks related and might cover it transitively.
- **Cover the full range the wording implies.** "Widened *or* narrowed", "first *or* anywhere" means multiple cases, not one representative.
- **Honour positional qualifiers literally.** "The *first* element after the header" needs an input satisfying exactly that, not one where the condition merely appears somewhere.
- **Extend the canonical existing test file** for a feature rather than creating a new narrower one.

### Deciding a test is not necessary

Not every line deserves a test. An unnecessary test is not free — it costs review attention, breaks on refactors, and dilutes the signal of the suite. Deciding *not* to test something is a legitimate, defensible outcome.

Ask what a plausible bug here would actually cost:

| Skip when | Because |
|---|---|
| The code has no observable effect — a value assigned and never read, a redundant local | Nothing downstream can change. If mutating it cannot break anything, a test cannot catch anything. Consider deleting the code instead. |
| It is a pass-through with no logic — auto-properties, simple getters, records, plain DTO mapping | There is no behaviour to pin down. A test here asserts the language works. |
| It is generated — `*.g.cs`, `wayd-api.ts`, migrations, `Internal/Generated/` | Not hand-written, and regenerating it would invalidate the test anyway. |
| The framework owns the behaviour — EF change tracking, Wolverine dispatch, `IEntityTypeConfiguration` wiring | Testing it tests the framework. Cover *your* logic that sits on top. |
| A guard is redundant behind an equivalent one already tested | Note the redundancy; do not duplicate the test. |

**Test regardless of how simple it looks when:**

- Money, scoring, dates, or status transitions are involved — a one-line `Status.Active` assignment is trivial code with expensive failure.
- It encodes a **business rule** rather than a mechanism. Rules change and get misremembered; a test is the record of what was decided.
- It is a boundary or a guard clause — the places off-by-one and null bugs actually live.
- It has broken before. A regression test earns its place by history.

**The deciding question is consequence, not line count.** "This code is simple" is not the reason to skip — `return _taxRate * amount;` is as simple as it gets and absolutely wants a test. The reason to skip is that a bug here would be either impossible or harmless.

When skipping something a reader might expect to be covered, **say so in the summary** — "no test for X; it has no observable effect" is a reviewable claim. Silence reads as an oversight.

---

## Part 3 — The verification gate

Run this **before reporting any test work complete**.

**Applies when:** five or more tests were added or changed, **or** the request enumerated specific behaviours to verify. Below that threshold, the self-review checklist alone is enough.

**Scope — the mutation loop (Step 2) is for .NET code covered by a unit project.** Every command below is `dotnet`, and mutating against a container-backed suite is prohibited. Two cases therefore skip Step 2 and run Steps 1, 3, 4 and 5 only:

| Change | What to run instead of Step 2 |
|---|---|
| **Integration-only** (Testcontainers suite) | Baseline green, the self-review checklist, and the CI-discovery check. Note in the summary that mutation verification was not applicable. |
| **React client** (Jest) | The Jest equivalent below, then the checklist and CI check. |

For a React change, the loop still works — only the commands differ. Baseline with `npm run test:ci`, mutate the component or hook under test, re-run the narrowest suite (`npx jest <path>`), and revert. The mutation catalogue's boundary, logic, and return-value rows apply unchanged; the .NET-specific rows (`SaveChanges`, status enums) do not.

### Step 1 — Baseline green

Run the narrowest project that covers the change and confirm it passes:

```bash
dotnet test "<path/to/Project.Tests.csproj>"
```

Record the pass count. Do not proceed from a red baseline — fix it or say so.

### Step 2 — Mutate, run, revert

For each substantive behaviour the new tests claim to cover, apply **one real edit** to the production code, re-run **only the covering tests**, then revert.

Mutation catalogue:

| Kind | Original | Mutation |
|---|---|---|
| Boundary | `<` / `>` | `<=` / `>=` |
| Boundary | `i < count` | `i <= count` |
| Boundary | `index + 1` | `index` |
| Logic | `&&` | `\|\|` |
| Logic | `!condition` | `condition` |
| Guard | `if (x is null) return Result.Failure(...)` | delete the guard |
| Return | `return result` | `return null` / `default` |
| Return | `return true` | `return false` |
| Collection | `return list` | `return []` |
| Persistence | `await SaveChanges(...)` | delete the call |
| Status | `Status.Active` | a neighbouring enum value |

Verdicts:

- **Killed** — a test went red. Good; that test has value. Revert and move on.
- **Survived** — everything stayed green. **This is a real gap.** Strengthen the assertion or add a test, then re-verify it now kills.
- **Equivalent** — the mutation cannot change behaviour given the domain (an impossible `==` case). Not a gap; skip it.

**Rules:**

1. One mutation at a time.
2. **Revert immediately after each check** — never leave a mutation in the working tree.
3. **Revert only the mutation, never the file.** Undo the exact edit you made — the inverse edit, or a targeted revert of that hunk. **Never `git checkout -- <file>`, `git restore`, `git stash`, or any whole-file reset**: the working tree may hold unrelated uncommitted work, and a whole-file revert destroys it. Assume it does.
4. Confirm the suite is green again before finishing, and that `git diff` shows only the changes you intended to keep.
5. Run the narrowest covering test, not the full suite. Never mutate against an integration project — each run starts a container.
6. Never mutate to "fix" a failing test. The mutation is a probe; production behaviour is not being changed.

### Step 3 — Report honestly, and under-claim

The commonest failure of this analysis is **telling someone their tests are weak when the tests actually catch the bug.**

- **Never claim a gap that was not verified by running.** If it was checked and killed, it is not a finding.
- If the suite could not be run, label every finding **unverified (static reasoning)** and downgrade its confidence. Do not present it as proven.
- **When most mutations are killed, lead with that.** "8 of 9 mutations caught; one gap in X" — not a HIGH RISK banner over a strong suite.
- **Rate by risk, not count.** One survived mutation in scoring or status-transition logic outweighs five in logging.
- **One test often kills several mutations.** Do not recommend a test per survivor.
- **Skip trivial code.** Auto-properties, simple getters, records, generated files (`*.g.cs`, migrations, `wayd-api.ts`) are not worth mutating.

State the observed kill count in the report so the claim is checkable.

### Step 4 — Self-review checklist

Always, regardless of threshold:

- [ ] Does every new test earn its place — would a bug it catches actually be observable and costly?
- [ ] Would emptying the function body fail every new test?
- [ ] Concrete values asserted, not just non-null / type checks?
- [ ] A secondary observable asserted where the operation touches more than its return value?
- [ ] `SaveChangesCallCount` asserted on both success and failure paths?
- [ ] No tautological round-trip assertions?
- [ ] `TestContext.Current.CancellationToken` on every cancellable call?
- [ ] `// Arrange` / `// Act` / `// Assert` on every test?
- [ ] One SUT per file, class name matching the file name?
- [ ] Fakers in the domain's own `Data/` folder, using `With{Property}` extensions?
- [ ] No new `Testcontainers` reference in a unit project?
- [ ] `[Collection(...)]` rather than `IClassFixture<T>` for integration tests?
- [ ] Every enumerated scenario from the request mapped to a test?

### Step 5 — Confirm CI will run it

A test CI never discovers has no value.

```bash
# The unit half — no Docker needed
./.github/scripts/dotnet-test-projects.sh unit

# The integration half — needs Docker running
./.github/scripts/dotnet-test-projects.sh integration
```

Confirm new tests appear in the expected half. A test that landed in the wrong half either needs Docker it will not get, or quietly moved a whole project across the CI split.

> `--filter` matches per assembly and exits non-zero when an assembly has no match, so a filtered run across the solution fails on every non-matching project. Filter one project at a time, or use the split script.

---

## Note — prioritizing where to test next

Everything above answers *"is this test worth keeping?"*. A different question — *"of all the untested code, what should be tackled first?"* — is worth a periodic sweep rather than a per-change check.

The **CRAP score** (Change Risk Anti-Patterns) ranks methods by combining cyclomatic complexity with coverage:

```
CRAP(m) = complexity² × (1 − coverage)³ + complexity
```

| Score | Reading |
|---|---|
| < 5 | Simple, or well covered |
| 5–15 | Acceptable |
| 15–30 | Wants tests or simplification |
| > 30 | High complexity and low coverage — attack first |

A fully covered method scores its own complexity (the floor); an uncovered one scores `complexity² + complexity`. The cubed coverage term means the first tests against a gnarly method drop the score sharply, which is what makes it a useful ranking.

**Treat it as a worklist generator, not a gate:**

- It needs a **Cobertura coverage run**. PRs deliberately skip coverage (instrumentation costs roughly 4× the test run) — only the main-branch build collects it. Use that output rather than paying for coverage locally on every change.
- It ranks .NET methods only; the React client and MCP server need separate judgement.
- **It cannot tell you whether a test is *necessary*.** It measures the shape of the code, not the cost of a bug. A complexity-1 method scores ~2 whether it is dead code or the tax calculation — the first genuinely needs no test, the second badly wants one. Use the *Deciding a test is not necessary* table for that call.
- Never chase the score for its own sake. Ranking high means "look here", not "add tests until the number falls". A method may be legitimately complex and adequately covered by a few strong tests.

Once it points at a method, the rest of this skill applies unchanged: write tests to the conventions, then run the gate to prove they kill mutations.

---

## Frontend tests

Jest with React Testing Library, in `Wayd.Web/src/wayd.web.reactclient`:

```bash
cd Wayd.Web/src/wayd.web.reactclient

npm test               # local watch-friendly run
npm run test:ci        # what CI runs on a PR
npx jest <path>        # a single suite, for the mutation loop
```

The `cd` matters: there is no `package.json` at the repository root, so these fail from there.

The same value rules apply: assert rendered output and behaviour, not implementation detail. Querying by role or visible text survives refactors that querying by class name does not.

**WaydGrid and other TanStack consumers carry a `'use no memo'` directive**, which opts them out of React Compiler memoization — so manual `useMemo`/`useCallback` in those components is deliberate, not a leftover to clean up. Grid tests also depend on `src/jest.setup.ts`, which mocks the `data-grid-body-viewport` rect to 800×600 — jsdom has no layout, and the virtualizer renders zero rows at zero height. With a 28px row estimate that yields a **32-row window** (22 visible + 10 overscan), so a grid test asserting on rendered rows sees that window, not the full data set.

---

## Anti-patterns to reject on sight

| Anti-pattern | Why it fails |
|---|---|
| Assertion-free test | Runs code, verifies nothing. Coverage lies. |
| `.Should().NotBeNull()` as the only assertion | Passes on an empty or default object. |
| Tautological round-trip | Asserts the test's own setup, not the code. |
| Swallowed exception (`try { … } catch { }`) | Test passes when the code throws. |
| `Thread.Sleep` to wait | Non-deterministic and slow. Mock the clock. |
| `DateTime.UtcNow` in a test | Flaky by construction. Use `IDateTimeProvider`. |
| `[Skip]`/`[Ignore]` to get green | Hides the failure. Fix it or delete it. |
| Duplicate tests differing only by input | Parameterize with `[Theory]`. |
| Asserting on "the only row" in an integration test | Integration tests share a database. |
| Missing `SaveChangesCallCount` on a guard test | Passes even if the handler saved bad state. |
