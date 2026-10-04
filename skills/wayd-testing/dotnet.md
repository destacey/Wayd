# .NET tests

What [docs/contributing/testing.mdx](../../docs/contributing/testing.mdx) does not already cover. Read that page first for the stack, layout, fakers, the fake DbContext, and the collection fixture.

## Contents

- [Handler test shape](#handler-test-shape)
- [Assertion style](#assertion-style)
- [The Testcontainers tripwire](#the-testcontainers-tripwire)
- [What a fake DbContext cannot see](#what-a-fake-dbcontext-cannot-see)

## Handler test shape

Adapted from `Wayd.Common/tests/Wayd.Common.Application.Tests/Sut/Scoring/ScoringModels/Commands/ActivateScoringModelCommandHandlerTests.cs`, which tests `ActivateScoringModelCommandHandler(IWaydDbContext, ICurrentUser, IDateTimeProvider, ILogger<…>)`:

```csharp
public class ActivateScoringModelCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 1, 15, 9, 30)));
    private readonly ScoringModelFaker _faker = new();

    private ActivateScoringModelCommandHandler CreateHandler() =>
        new(_dbContext, _currentUser.Object, _dateTimeProvider, NullLogger<ActivateScoringModelCommandHandler>.Instance);

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

    [Fact]
    public async Task Handle_ShouldFail_WhenModelNotFound()
    {
        // Arrange
        _dbContext.ScoringModels.Add(_faker.AsProposedWsjf());
        var command = new ActivateScoringModelCommand(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
```

The points it demonstrates:

- **`SaveChangesCallCount` on both paths.** `Be(1)` where the handler should persist, `Be(0)` where a guard rejects. A failure test that omits it passes even if the handler saved the bad state.
- **Handlers return `Result`/`Result<T>`.** Assert on `IsSuccess`/`IsFailure` and the error text, not on thrown exceptions. Passing `result.Error` as the `because` argument makes a failure self-diagnosing.
- **Time is a `TestingDateTimeProvider`** (`Wayd.Tests.Shared`) over a NodaTime `FakeClock`, pinned to a fixed `Instant`. Call `Advance(Duration)` to move it. Never `DateTime.UtcNow` — a test on wall-clock time is flaky by construction.
- **Construct the handler explicitly** in a `CreateHandler()` helper, so a new constructor dependency fails to compile in one place.

## Assertion style

FluentAssertions (`.Should()…`) is preferred for **new** test files. When extending an existing file, follow that file's style — xUnit `Assert` is common in older suites (`Wayd.Web.Api.IntegrationTests` is almost entirely `Assert`). Do not convert a file's existing assertions as a side effect of adding a test.

## The Testcontainers tripwire

testing.mdx explains the two traits. What matters when writing tests:

**Adding a `Testcontainers*` package reference silently moves every test in that project into the integration half**, which needs Docker and runs separately. Never add one to make a single test work — put that test in the area's existing `*.IntegrationTests` project. Never hand-tag `Category` or `Requires`; both are derived in `Directory.Build.targets`.

`Category=Integration` does not mean "needs Docker". `Wayd.Integrations.AzureDevOps.IntegrationTests` is `Integration` but references no Testcontainers package, so it runs in the unit half; it calls a live Azure DevOps organization and skips itself (`Assert.SkipUnless` on the fixture's `IsConfigured`) when no organization URL and PAT are configured, which is the case in CI.

## What a fake DbContext cannot see

The fakes back each `DbSet` with an in-memory list. Anything that depends on EF's query translation or change tracker is invisible to them — and therefore to the mutation gate, which runs against unit projects. A mutation that survives here survives for a reason that has nothing to do with assertion strength.

### A missing `.Include`

`.Include` is a no-op in memory, so deleting one changes no unit-test outcome. The case that matters is PPM authorization: a handler mutating a project, program, or portfolio must load the record's own roles **and** its ancestors' (the rule and the required chain are in [AGENTS.md](../../AGENTS.md) under Authorization). Omitting `.Include(p => p.Roles)` denies an Owner or Manager on the record itself; omitting an ancestor's denies everyone who leads from above. `UpdateProjectCommand.cs` has the full chain.

So this is a **read-the-query check, not a test check**:

- When touching a PPM mutating handler, verify the `.Include` chain by eye.
- Cover the authorization rule itself in the aggregate's domain tests, where roles are real objects.
- Creation and import paths are exempt — they pass `PpmActor.System`, and no ancestry is expected. Do not add leadership assertions to them.

### Change-tracker behaviour

What EF will insert, update, or ignore at save is also unmodelled by the fakes: an untracked entity reached through a navigation and inserted on save, a handler that rewrote a row with identical values, a previous value read off a navigation that was never loaded. These need a Testcontainers test in the area's integration project.

`SavedEntityRecorder` (`Wayd.ProjectPortfolioManagement.IntegrationTests/Infrastructure/`) is a `SaveChangesInterceptor` that records the type and `EntityState` of every entity written, as EF saw it at save. Use it to prove a handler left an entity alone — reading the row back cannot, because an update that rewrote the same values looks untouched. See `ChangeProjectLifecycleCommandHandlerTests` for its use.
