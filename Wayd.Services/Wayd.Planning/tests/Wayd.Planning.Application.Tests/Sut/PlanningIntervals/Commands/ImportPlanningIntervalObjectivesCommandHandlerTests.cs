using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

public sealed class ImportPlanningIntervalObjectivesCommandHandlerTests : IDisposable
{
    private static readonly Guid PlanningIntervalId = Guid.CreateVersion7();

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PlanningIntervalObjectiveImportDefinition _definition;

    public ImportPlanningIntervalObjectivesCommandHandlerTests()
    {
        _definition = new PlanningIntervalObjectiveImportDefinition(
            _dbContext,
            Mock.Of<IDateTimeProvider>(),
            Mock.Of<ICurrentUser>(),
            new ImportPayloadSerializer());

        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Guid.CreateVersion7()));
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportPlanningIntervalObjectivesCommandHandler CreateHandler() =>
        new(new ImportDefinitionRegistry([_definition]), _dispatcher.Object);

    private static SubmittedImportRow<ImportPlanningIntervalObjectiveDto> Row(
        string? importId = "r1", Guid? intervalId = null) =>
        new(importId, new ImportPlanningIntervalObjectiveDto(
            intervalId ?? PlanningIntervalId,
            Guid.CreateVersion7(),
            "Ship the thing",
            null,
            ObjectiveStatus.NotStarted,
            0,
            null,
            null,
            false,
            null,
            null));

    private Task<Result<Guid>> Handle(params SubmittedImportRow<ImportPlanningIntervalObjectiveDto>[] rows) =>
        CreateHandler().Handle(
            new ImportPlanningIntervalObjectivesCommand(rows),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsTheRunAgainstThePlanningIntervalObjectiveImport()
    {
        // Arrange & Act
        var result = await Handle(Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(
                    c => c.ImportType == PlanningIntervalObjectiveImportDefinition.ImportKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CarriesTheSubmissionGroupThroughToTheRun()
    {
        // Arrange — a seed submitting this file alongside others
        var groupId = Guid.CreateVersion7();

        // Act
        await CreateHandler().Handle(
            new ImportPlanningIntervalObjectivesCommand([Row()], groupId),
            TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.SubmissionGroupId == groupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SubmitsAFileSpanningManyPlanningIntervalsAsOneRun()
    {
        // Arrange — an onboarding file carrying the history of several intervals at once
        var elsewhere = Guid.CreateVersion7();

        // Act
        var result = await Handle(Row("r1"), Row("r2", intervalId: elsewhere));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.Rows.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ForAnEmptyFile_FailsWithoutSubmitting()
    {
        // Arrange & Act
        var result = await Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_RejectsARowWhoseDataIsInvalid()
    {
        // Arrange — an empty name is the DTO's own rule, and nothing else was checking it
        var bad = Row() with { Data = Row().Data with { Name = string.Empty } };

        // Act
        var result = new ImportPlanningIntervalObjectivesCommandValidator()
            .Validate(new ImportPlanningIntervalObjectivesCommand([bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_RejectsARowWithoutAPlanningInterval()
    {
        // Arrange — the row is the only place the interval is named, so a blank cell is a malformed file
        var bad = Row(intervalId: Guid.Empty);

        // Act
        var result = new ImportPlanningIntervalObjectivesCommandValidator()
            .Validate(new ImportPlanningIntervalObjectivesCommand([bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAWellFormedRow()
    {
        // Arrange & Act
        var result = new ImportPlanningIntervalObjectivesCommandValidator()
            .Validate(new ImportPlanningIntervalObjectivesCommand([Row()]));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
