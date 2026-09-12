using CSharpFunctionalExtensions;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals.Commands;

/// <summary>
/// Submitting a planning interval import. The command's own work is the rule that belongs to the file
/// rather than to any row — a name may appear once — and handing the rows over as stored payloads.
/// </summary>
public sealed class ImportPlanningIntervalsCommandTests : IDisposable
{
    private static readonly LocalDate Start = new(2026, 1, 5);
    private static readonly LocalDate End = new(2026, 2, 15);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PlanningIntervalImportDefinition _definition;

    private readonly ImportPlanningIntervalsCommandValidator _validator = new();

    private SubmitImportCommand? _submitted;

    public ImportPlanningIntervalsCommandTests()
    {
        _definition = new PlanningIntervalImportDefinition(_dbContext, new ImportPayloadSerializer());

        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            // IDispatcher.Send takes ICommand<Guid>, so the callback has to be declared against that
            .Callback<ICommand<Guid>, CancellationToken>((command, _) => _submitted = command as SubmitImportCommand)
            .ReturnsAsync(Result.Success(Guid.CreateVersion7()));
    }

    public void Dispose() => _dbContext.Dispose();

    private static ImportPlanningIntervalDto Interval(string name = "PI 2026.1") =>
        new(name, "The first interval of the year", Start, End, 2, "PI26.1-", [Guid.CreateVersion7()]);

    private static SubmittedImportRow<ImportPlanningIntervalDto> Row(
        string? importId = "r1", string name = "PI 2026.1") =>
        new(importId, Interval(name));

    private Task<Result<Guid>> Handle(
        Guid? submissionGroupId = null, params SubmittedImportRow<ImportPlanningIntervalDto>[] rows) =>
        new ImportPlanningIntervalsCommandHandler(
                new ImportDefinitionRegistry([_definition]), _dispatcher.Object)
            .Handle(new ImportPlanningIntervalsCommand(rows, submissionGroupId), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsTheRunAgainstThePlanningIntervalImport()
    {
        // Arrange & Act
        var result = await Handle(null, Row(name: "PI 2026.1"), Row("r2", "PI 2026.2"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _submitted!.ImportType.Should().Be(PlanningIntervalImportDefinition.ImportKey);
        _submitted.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_StoresEachRowAsThePayloadTheDefinitionReadsBack()
    {
        // Arrange — the payload is written now and read by a worker that may run days later, so the one
        // the definition would serialize is what has to reach the run
        var interval = Interval();

        // Act
        await Handle(null, new SubmittedImportRow<ImportPlanningIntervalDto>("r1", interval));

        // Assert
        _submitted!.Rows.Single().Payload.Should().Be(_definition.SerializeRow(interval));
    }

    [Fact]
    public async Task Handle_CarriesTheSubmissionGroupOntoTheRun()
    {
        // Arrange
        var group = Guid.CreateVersion7();

        // Act
        await Handle(group, Row());

        // Assert — a set of files posted under one value shows as one batch
        _submitted!.SubmissionGroupId.Should().Be(group);
    }

    [Fact]
    public async Task Handle_FailsWhenTheFileHasNoRows()
    {
        // Arrange & Act
        var result = await Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no planning intervals");
        _dispatcher.Verify(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FailsWhenTheImportIsNotRegistered()
    {
        // Arrange — an empty registry stands in for a definition that was never registered
        var handler = new ImportPlanningIntervalsCommandHandler(
            new ImportDefinitionRegistry([]), _dispatcher.Object);

        // Act
        var result = await handler.Handle(
            new ImportPlanningIntervalsCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.Verify(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Validator_RejectsAFileRepeatingAName()
    {
        // Arrange — a planning interval's name is unique, so the file is ambiguous whatever already exists
        var command = new ImportPlanningIntervalsCommand([Row(name: "PI 2026.1"), Row("r2", "PI 2026.1")]);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Rows)
            .WithErrorMessage("Planning interval Name must be unique within the file.");
    }

    [Fact]
    public void Validator_ComparesNamesWithoutCaseOrSurroundingWhitespace()
    {
        // Arrange — the definition trims and compares case-insensitively, so this file is the same clash
        var command = new ImportPlanningIntervalsCommand([Row(name: "PI 2026.1"), Row("r2", " pi 2026.1 ")]);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Rows);
    }

    [Fact]
    public void Validator_RejectsAnEmptyFile()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(new ImportPlanningIntervalsCommand([]))
            .ShouldHaveValidationErrorFor(c => c.Rows);
    }

    [Fact]
    public void Validator_AcceptsAFileOfDistinctNames()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(new ImportPlanningIntervalsCommand([Row(name: "PI 2026.1"), Row("r2", "PI 2026.2")]))
            .ShouldNotHaveAnyValidationErrors();
    }
}
