using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Imports;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Imports;

public sealed class PlanningIntervalObjectiveImportDefinitionTests : IDisposable
{
    private const int CreatePass = 0;

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PlanningIntervalObjectiveImportDefinition _definition;

    private readonly PlanningInterval _interval;
    private readonly PlanningTeam _team;

    private Guid _createdObjectiveId = Guid.CreateVersion7();

    public PlanningIntervalObjectiveImportDefinitionTests()
    {
        _definition = new PlanningIntervalObjectiveImportDefinition(
            _dbContext,
            _dispatcher.Object,
            NullLogger<PlanningIntervalObjectiveImportDefinition>.Instance,
            new ImportPayloadSerializer());

        _interval = new PlanningIntervalFaker().Generate();
        _team = new PlanningTeamFaker(TeamType.Team).Generate();

        _dbContext.AddPlanningInterval(_interval);
        _dbContext.AddPlanningTeam(_team);

        _dispatcher
            .Setup(d => d.Send(It.IsAny<ImportObjectiveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_createdObjectiveId));
        _dispatcher
            .Setup(d => d.Send(It.IsAny<DeleteObjectiveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportPlanningIntervalObjectiveDto Row(Guid? intervalId = null, Guid? teamId = null) =>
        new(
            intervalId ?? _interval.Id,
            teamId ?? _team.Id,
            "Ship the thing",
            null,
            ObjectiveStatus.NotStarted,
            0,
            null,
            null,
            false,
            null,
            null);

    private Task<Result<ImportPassResult>> Run(params ImportPlanningIntervalObjectiveDto[] objectives) =>
        _definition.ExecutePass(
            Guid.CreateVersion7(),
            CreatePass,
            [.. objectives.Select((o, i) => ImportProcessRow.Create($"r{i + 1}", i + 1, _definition.SerializeRow(o)))],
            isFinalChunk: true,
            TestContext.Current.CancellationToken);

    [Fact]
    public void Definition_IsPerRowBecauseTheObjectiveIsCommittedElsewhere()
    {
        // Arrange & Act & Assert — the objective is created by dispatching into Goals, which commits in
        // its own scope, so the runner's discard could never take it back
        _definition.Atomicity.Should().Be(ImportAtomicity.PerRow);
    }

    [Fact]
    public async Task CreateObjectives_AttachesTheObjectiveToThePlanningInterval()
    {
        // Arrange & Act
        var result = await Run(Row());

        // Assert
        result.Value.Rows.Single().Failed.Should().BeFalse();
        result.Value.Rows.Single().CreatedEntityId.Should().Be(_createdObjectiveId);
        _interval.Objectives.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateObjectives_RejectsARowNamingAnIntervalThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row(intervalId: Guid.CreateVersion7()));

        // Assert — rejected before anything is created in Goals
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(It.IsAny<ImportObjectiveCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateObjectives_RejectsARowNamingATeamThatDoesNotExist()
    {
        // Arrange & Act
        var result = await Run(Row(teamId: Guid.CreateVersion7()));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(It.IsAny<ImportObjectiveCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateObjectives_RejectsEveryRowWhenObjectivesAreLocked()
    {
        // Arrange — a locked interval refuses new objectives, and the row should say so rather than
        // create one in Goals and fail attaching it
        var locked = new PlanningIntervalFaker().WithObjectivesLocked(true).Generate();
        _dbContext.AddPlanningInterval(locked);

        // Act
        var result = await Run(Row(intervalId: locked.Id));

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().Contain("locked");
    }

    [Fact]
    public async Task CreateObjectives_WhenTheObjectiveCannotBeCreated_RejectsTheRow()
    {
        // Arrange
        _dispatcher
            .Setup(d => d.Send(It.IsAny<ImportObjectiveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("Goals said no."));

        // Act
        var result = await Run(Row());

        // Assert
        result.Value.Rows.Single().Failed.Should().BeTrue();
        result.Value.Rows.Single().Error.Should().Contain("Goals said no.");
    }

    [Fact]
    public async Task CreateObjectives_KeepsTheGoodRowsWhenOneIsRejected()
    {
        // Arrange — per row, so one bad row does not take the file down with it
        var good = Row();
        var bad = Row(teamId: Guid.CreateVersion7());

        // Act
        var result = await Run(good, bad);

        // Assert
        result.Value.Rows.Single(r => r.ImportId == "r1").Failed.Should().BeFalse();
        result.Value.Rows.Single(r => r.ImportId == "r2").Failed.Should().BeTrue();
        _interval.Objectives.Should().ContainSingle();
    }
}
