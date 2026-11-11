using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server, where each team's sprints and its running sprint's work item history are read
/// as the sprint card reads them.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetTeamsSprintSignalQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Instant At(int month, int day, int hour = 0) => Instant.FromUtc(2026, month, day, hour, 0);

    private async Task<IReadOnlyDictionary<Guid, Application.Iterations.Dtos.TeamSprintSignalDto>> Handle(params Guid[] teamIds)
    {
        var dispatcher = HolidayDispatcher.With();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var schedulingSettings = Mock.Of<ISettings<SchedulingSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new SchedulingSettings()));
        var generalSettings = Mock.Of<ISettings<GeneralSettings>>(s => s.Get(It.IsAny<CancellationToken>()) == Task.FromResult(new GeneralSettings()));

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetTeamsSprintSignalQueryHandler(accessor.Context, dispatcher.Object, schedulingSettings, generalSettings,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        return await handler.Handle(new GetTeamsSprintSignalQuery(teamIds), Ct);
    }

    [Fact]
    public async Task Handle_SprintStartedAndLeftOpenPastItsEnd_IsEndedNotCompleted()
    {
        // Arrange — ForecastSeeder.Now is 22 September 2026
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        var leftOpen = await _seed.AddSprint(team.Id, new LocalDate(2026, 9, 1), Ct);
        await _seed.AddSprint(team.Id, new LocalDate(2026, 9, 15), Ct);
        await _seed.RecordSprintDates(leftOpen, At(9, 1, 9), null, Ct);

        var completedTeam = await _seed.AddTeam(Ct);
        var completed = await _seed.AddSprint(completedTeam.Id, new LocalDate(2026, 9, 1), Ct);
        await _seed.RecordSprintDates(completed, At(9, 1, 9), At(9, 14, 17), Ct);

        // Act
        var result = await Handle(team.Id, completedTeam.Id);

        // Assert
        result.Keys.Should().Equal(team.Id);
        var signal = result[team.Id];
        signal.Sprint.Id.Should().Be(leftOpen);
        signal.EndedNotCompleted.Should().BeTrue();
        signal.Status.Should().Be(AttentionStatus.NeedsAttention);
        signal.ExpectedShare.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RunningSprint_MeasuresWhatIsDoneAgainstItsCommitment()
    {
        // Arrange — both sprints run 15 to 28 September with two items committed; it is now the 22nd, and the
        // history is read in full, so health is measured against the commitment
        await _seed.Reset(Ct);
        await _seed.MarkHistoryReadToEnd(Ct);
        var halfDone = await _seed.AddTeam(Ct);
        var halfDoneSprint = await _seed.AddSprint(halfDone.Id, new LocalDate(2026, 9, 15), Ct);
        var finished = await _seed.AddItem(halfDone.Id, Ct, StatusCategory.Done, iterationId: halfDoneSprint);
        await _seed.AddPeriod(finished.Id, 1, At(9, 10), At(9, 17, 12), StatusCategory.Active, Ct, iterationId: halfDoneSprint);
        await _seed.AddPeriod(finished.Id, 2, At(9, 17, 12), null, StatusCategory.Done, Ct, iterationId: halfDoneSprint);
        var open = await _seed.AddItem(halfDone.Id, Ct, StatusCategory.Active, iterationId: halfDoneSprint);
        await _seed.AddPeriod(open.Id, 1, At(9, 10), null, StatusCategory.Active, Ct, iterationId: halfDoneSprint);
        // Added after the commitment point, so it counts toward the sprint's work now but not its commitment.
        var added = await _seed.AddItem(halfDone.Id, Ct, StatusCategory.Active, iterationId: halfDoneSprint);
        await _seed.AddPeriod(added.Id, 1, At(9, 18), null, StatusCategory.Active, Ct, iterationId: halfDoneSprint);

        var nothingDone = await _seed.AddTeam(Ct);
        var nothingDoneSprint = await _seed.AddSprint(nothingDone.Id, new LocalDate(2026, 9, 15), Ct);
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var item = await _seed.AddItem(nothingDone.Id, Ct, StatusCategory.Active, iterationId: nothingDoneSprint);
            await _seed.AddPeriod(item.Id, 1, At(9, 10), null, StatusCategory.Active, Ct, iterationId: nothingDoneSprint);
        }

        var between = await _seed.AddTeam(Ct);
        await _seed.AddSprint(between.Id, new LocalDate(2026, 8, 1), Ct);

        // Act
        var result = await Handle(halfDone.Id, nothingDone.Id, between.Id);

        // Assert
        result.Keys.Should().BeEquivalentTo([halfDone.Id, nothingDone.Id]);

        var ahead = result[halfDone.Id];
        ahead.Sprint.Id.Should().Be(halfDoneSprint);
        ahead.EndedNotCompleted.Should().BeFalse();
        ahead.DoneShare.Should().Be(0.5);
        ahead.ExpectedShare.Should().BeInRange(0.3, 0.6);
        ahead.Status.Should().Be(AttentionStatus.OnTrack);

        var behind = result[nothingDone.Id];
        behind.DoneShare.Should().Be(0);
        behind.ExpectedShare.Should().Be(ahead.ExpectedShare);
        behind.Status.Should().Be(AttentionStatus.NeedsAttention);
    }

    [Fact]
    public async Task Handle_HistoryNotReadInFull_MeasuresTheWorkInTheSprintNowAgainstItsDaysGone()
    {
        // Arrange — the sprint runs 15 to 28 September, 13 days from its first to its last; 7 are gone on the 22nd
        await _seed.Reset(Ct);
        var team = await _seed.AddTeam(Ct);
        var sprint = await _seed.AddSprint(team.Id, new LocalDate(2026, 9, 15), Ct);
        await _seed.AddItem(team.Id, Ct, StatusCategory.Done, iterationId: sprint);
        await _seed.AddItem(team.Id, Ct, StatusCategory.Removed, iterationId: sprint);
        await _seed.AddItem(team.Id, Ct, StatusCategory.Active, iterationId: sprint);
        await _seed.AddItem(team.Id, Ct, StatusCategory.Proposed, iterationId: sprint);
        await _seed.AddItem(team.Id, Ct, StatusCategory.Done, epic: true, iterationId: sprint);

        // Act
        var result = await Handle(team.Id);

        // Assert
        var signal = result[team.Id];
        signal.Sprint.Id.Should().Be(sprint);
        signal.DoneShare.Should().Be(0.5);
        signal.ExpectedShare.Should().BeApproximately(7.0 / 13, 1e-9);
        signal.Status.Should().Be(AttentionStatus.OnTrack);
    }
}
