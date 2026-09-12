using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;

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
            _dispatcher.Object,
            NullLogger<PlanningIntervalObjectiveImportDefinition>.Instance,
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
            new ImportPlanningIntervalObjectivesCommand(PlanningIntervalId, rows),
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
            new ImportPlanningIntervalObjectivesCommand(PlanningIntervalId, [Row()], groupId),
            TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.SubmissionGroupId == groupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RefusesAFileSpanningMorePlanningIntervalsThanOne()
    {
        // Arrange — the rule this import has of its own. It used to be a route parameter mismatch in the
        // controller, which described the request rather than the rule.
        var elsewhere = Guid.CreateVersion7();

        // Act
        var result = await Handle(Row("r1"), Row("r2", intervalId: elsewhere));

        // Assert — named per row, so the person knows which line to fix
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("r2").And.Contain(elsewhere.ToString());
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RefusesBeforeSubmittingAnything()
    {
        // Arrange & Act — a mixed file is rejected outright rather than queued and half applied
        await Handle(Row("r1"), Row("r2", intervalId: Guid.CreateVersion7()));

        // Assert
        _dispatcher.Verify(
            d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
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
            .Validate(new ImportPlanningIntervalObjectivesCommand(PlanningIntervalId, [bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAWellFormedRow()
    {
        // Arrange & Act
        var result = new ImportPlanningIntervalObjectivesCommandValidator()
            .Validate(new ImportPlanningIntervalObjectivesCommand(PlanningIntervalId, [Row()]));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NamesTheOffendingRowByPositionWhenItHasNoImportId()
    {
        // Arrange — the column is optional, so the message must not read "Row ''"
        var elsewhere = Guid.CreateVersion7();
        var rows = new[]
        {
            Row(importId: null),
            Row(importId: null!, intervalId: elsewhere),
        };

        // Act
        var result = await CreateHandler().Handle(
            new ImportPlanningIntervalObjectivesCommand(PlanningIntervalId, rows),
            TestContext.Current.CancellationToken);

        // Assert — the same key the run would give it, so the two agree
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Row '2'");
    }
}
