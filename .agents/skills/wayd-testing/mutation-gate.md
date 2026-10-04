# The mutation gate

Proves each new or changed test fails when the code it covers is wrong. Run it before reporting test work complete.

## Contents

- [When it applies](#when-it-applies)
- [Where it can run](#where-it-can-run)
- [The loop](#the-loop)
- [Mutation catalogue](#mutation-catalogue)
- [Reverting safely](#reverting-safely)
- [Results table](#results-table)
- [Reporting](#reporting)

## When it applies

Whenever the change adds or alters an asserted behaviour, or the request enumerated behaviours to verify. A change that only renames, reformats, or moves tests needs the self-review checklist alone.

**Scope: one mutation per asserted behaviour.** Take the Return and Guard rows of the catalogue first — they are the cheapest to apply and the most often survived. One test frequently kills several mutations; do not add a test per mutation.

Skip code where a bug is impossible or harmless: auto-properties, records, plain mapping, generated files (`*.g.cs`, `wayd-api.ts`, migrations, `Internal/Generated/`).

## Where it can run

Only against a `Category=Unit` project, or a single Jest suite (baseline `npm run test:ci`, then `npx jest <path>` per mutation, from `Wayd.Web/src/wayd.web.reactclient`).

| Change covered by | Instead of the loop |
|---|---|
| A Testcontainers integration project | Baseline green, the self-review checklist, and the CI-half check. Each run starts a SQL Server container, so do not mutate against it. State in the report that the mutation gate was not applicable. |
| `Wayd.Integrations.AzureDevOps.IntegrationTests` | The same. It calls a live Azure DevOps organization, and where none is configured every test is skipped — a skipped test kills nothing. |
| A fake DbContext, for a `.Include` or change-tracker behaviour | Read the query by eye. `.Include` is a no-op in memory and the fakes model no change tracker, so the mutation survives whatever the assertions say — it is not an assertion-strength finding. |

## The loop

1. **Baseline.** Run the narrowest project covering the change and record the pass count. Do not proceed from red — fix it or say so.

   ```bash
   dotnet test "<path/to/Project.Tests.csproj>"
   ```

2. **Pick the next behaviour** a new or changed test claims to cover, and choose a mutation for it from the catalogue.
3. **Mutate.** Apply one real edit to the production code. One mutation at a time.
4. **Run only the covering tests**, filtered within that one project:

   ```bash
   dotnet test "<path/to/Project.Tests.csproj>" --filter "FullyQualifiedName~<TestClass>"
   ```

5. **Read the verdict.**
   - **Killed** — a test went red. Revert the mutation, record it, go to step 2.
   - **Survived** — everything stayed green. Revert the mutation, strengthen the assertion or add a test, then re-apply **the same mutation** and run again. Repeat until it is killed.
   - **Equivalent** — the mutation cannot change behaviour given the domain (an impossible `==` case). Revert, record it as equivalent, go to step 2.
6. **When every behaviour is done**, re-run the baseline command and confirm the pass count (plus any tests you added) is green.
7. **Check `git diff`** shows only the test changes you intend to keep, and no trace of a mutation.

Never mutate to make a failing test pass. The mutation is a probe; production behaviour is not being changed.

## Mutation catalogue

| Kind | Original | Mutation |
|---|---|---|
| Return | `return result` | `return null` / `default` |
| Return | `return true` | `return false` |
| Guard | `if (x is null) return Result.Failure(...)` | delete the guard |
| Boundary | `<` / `>` | `<=` / `>=` |
| Boundary | `index + 1` | `index` |
| Logic | `&&` | `\|\|` |
| Logic | `!condition` | `condition` |
| Collection | `return list` | `return []` |
| Persistence | `await _waydDbContext.SaveChangesAsync(...)` | delete the call |
| Status | `Status.Active` | a neighbouring enum value |

## Reverting safely

**Undo only the mutation, never the file.** Apply the inverse edit to the exact text you changed. Never `git checkout -- <file>`, `git restore`, `git stash`, or any whole-file reset: the working tree may hold unrelated uncommitted work, and a whole-file revert destroys it. Assume it does.

## Results table

Fill one row per mutation and put the table in the report, so the claim is checkable:

```markdown
| Mutation | File:line | Verdict | Killing test |
|---|---|---|---|
| Deleted the not-found guard | ActivateScoringModelCommand.cs:37 | Killed | Handle_ShouldFail_WhenModelNotFound |
| <mutation> | <file>:<line> | Survived → killed after strengthening | <test that now kills it> |
| <mutation> | <file>:<line> | Equivalent (<why it cannot change behaviour>) | — |
```

## Reporting

The commonest failure of this analysis is **telling someone their tests are weak when the tests actually catch the bug.**

- **Never claim a gap that was not verified by running.** Checked and killed is not a finding.
- If the suite could not be run, label every finding **unverified (static reasoning)** and lower its confidence.
- **When most mutations are killed, lead with that** — "8 of 9 mutations killed; one gap in X", not a high-risk banner over a strong suite.
- **Rate by risk, not count.** One survivor in scoring or status-transition logic outweighs five in logging.
