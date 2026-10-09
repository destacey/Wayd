using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// A sprint's team days off are one column behind a hand-written converter on a backing field; only a real
/// provider shows they are saved, read back in order, and recorded.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SetSprintTeamDaysOffCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate SprintStart = new(2026, 9, 14);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_SavesTheDaysOffAndReadsThemBack()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sprintId = await SeedSprint(cancellationToken);
        LocalDate[] daysOff = [SprintStart.PlusDays(8), SprintStart.PlusDays(4)];

        // Act
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var result = await Handler(accessor).Handle(new SetSprintTeamDaysOffCommand(sprintId, daysOff), cancellationToken);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var sprint = await verify.Context.Iterations.AsNoTracking().SingleAsync(i => i.Id == sprintId, cancellationToken);
        sprint.TeamDaysOff.Should().Equal(SprintStart.PlusDays(4), SprintStart.PlusDays(8));
        (await verify.Context.ActivityLogs.AnyAsync(a => a.AggregateId == sprintId && a.EventType == "SprintTeamDaysOffChangedEvent", cancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ClearingTheDaysOff_ReadsBackEmpty()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var sprintId = await SeedSprint(cancellationToken);
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            (await Handler(accessor).Handle(new SetSprintTeamDaysOffCommand(sprintId, [SprintStart.PlusDays(4)]), cancellationToken))
                .IsSuccess.Should().BeTrue();
        }

        // Act
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var result = await Handler(accessor).Handle(new SetSprintTeamDaysOffCommand(sprintId, []), cancellationToken);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var sprint = await verify.Context.Iterations.AsNoTracking().SingleAsync(i => i.Id == sprintId, cancellationToken);
        sprint.TeamDaysOff.Should().BeEmpty();
    }

    private static SetSprintTeamDaysOffCommandHandler Handler(WaydDbContextAccessor accessor)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        // System callers hold every permission, so this exercises the save rather than the membership check.
        var currentPrincipal = new Mock<ICurrentPrincipal>();
        currentPrincipal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new SetSprintTeamDaysOffCommandHandler(
            accessor.Context,
            Mock.Of<IDispatcher>(),
            currentUser.Object,
            currentPrincipal.Object,
            Mock.Of<IDateTimeProvider>(p => p.Now == SqlServerDbContextFixture.FixedNow),
            NullLogger<SetSprintTeamDaysOffCommandHandler>.Instance);
    }

    private async Task<Guid> SeedSprint(CancellationToken cancellationToken)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"D{key}"), TeamType.Team, true), SqlServerDbContextFixture.FixedNow);
        var sprint = Iteration.Create("Sprint", IterationType.Sprint,
            new IterationDateRange(SprintStart, SprintStart.PlusDays(13)), team.Id,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        context.Context.Iterations.Add(sprint);
        await context.Context.SaveChangesAsync(cancellationToken);

        return sprint.Id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
