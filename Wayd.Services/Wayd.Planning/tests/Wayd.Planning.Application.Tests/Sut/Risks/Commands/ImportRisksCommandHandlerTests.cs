using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Imports;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Commands;

public sealed class ImportRisksCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly RiskImportDefinition _definition;

    public ImportRisksCommandHandlerTests()
    {
        _definition = new RiskImportDefinition(_dbContext, new ImportPayloadSerializer());

        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(Guid.CreateVersion7()));
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportRisksCommandHandler CreateHandler(params IImportDefinition[] definitions) =>
        new(new ImportDefinitionRegistry(definitions.Length == 0 ? [_definition] : definitions),
            _dispatcher.Object);

    private static SubmittedImportRow<ImportRiskDto> Row(string? importId = "r1") =>
        new(importId, new ImportRiskDto(
            "A risk worth recording",
            null,
            Guid.CreateVersion7(),
            Instant.FromUtc(2026, 6, 1, 9, 0, 0),
            Guid.CreateVersion7(),
            RiskStatus.Open,
            RiskCategory.Owned,
            RiskGrade.Medium,
            RiskGrade.Medium,
            null,
            null,
            null,
            null));

    private Task<CSharpFunctionalExtensions.Result<Guid>> Handle(
        params SubmittedImportRow<ImportRiskDto>[] rows) =>
        CreateHandler().Handle(new ImportRisksCommand(rows), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsTheRunAgainstTheRiskImport()
    {
        // Arrange & Act
        var result = await Handle(Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.ImportType == RiskImportDefinition.ImportKey),
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
            new ImportRisksCommand([Row()], groupId), TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.SubmissionGroupId == groupId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SerializesEachRowThroughTheDefinition()
    {
        // Arrange — the controller hands over typed rows and never learns how a payload is stored
        SubmitImportCommand? captured = null;
        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand<Guid> c, CancellationToken _) => captured = (SubmitImportCommand)c)
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(Guid.CreateVersion7()));

        // Act
        await Handle(Row("r1"), Row("r2"));

        // Assert
        captured!.Rows.Select(r => r.ImportId).Should().Equal("r1", "r2");
        captured.Rows.Should().AllSatisfy(r => r.Payload.Should().Contain("A risk worth recording"));
    }

    [Fact]
    public async Task Handle_CarriesTheCallersKeyThrough()
    {
        // Arrange — results are reported against it, so it must survive submission unchanged
        SubmitImportCommand? captured = null;
        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand<Guid> c, CancellationToken _) => captured = (SubmitImportCommand)c)
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(Guid.CreateVersion7()));

        // Act
        await Handle(Row("EMP-0042"));

        // Assert
        captured!.Rows.Single().ImportId.Should().Be("EMP-0042");
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
    public async Task Handle_WhenTheDefinitionIsNotRegistered_Fails()
    {
        // Arrange — a deployment missing the definition should say so rather than persist a run nothing
        // can apply
        var handler = new ImportRisksCommandHandler(new ImportDefinitionRegistry([]), _dispatcher.Object);

        // Act
        var result = await handler.Handle(
            new ImportRisksCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_RejectsAFileWithNoRows()
    {
        // Arrange — on the pipeline, so it holds for any caller rather than only the endpoint that
        // validates its request model first
        var validator = new ImportRisksCommandValidator(
            new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0))));

        // Act
        var result = validator.Validate(new ImportRisksCommand([]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_RejectsARowWhoseDataIsInvalid()
    {
        // Arrange — an empty summary is the DTO's own rule, and nothing else was checking it
        var validator = new ImportRisksCommandValidator(
            new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0))));
        var bad = Row() with { Data = Row().Data with { Summary = string.Empty } };

        // Act
        var result = validator.Validate(new ImportRisksCommand([bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAWellFormedRow()
    {
        // Arrange
        var validator = new ImportRisksCommandValidator(
            new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0))));

        // Act
        var result = validator.Validate(new ImportRisksCommand([Row()]));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
