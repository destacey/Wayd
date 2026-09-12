using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Models;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class ImportEmployeesCommandHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly EmployeeImportDefinition _definition;

    public ImportEmployeesCommandHandlerTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));
        _definition = new EmployeeImportDefinition(_dbContext, clock, new ImportPayloadSerializer());

        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(Guid.CreateVersion7()));
    }

    private ImportEmployeesCommandHandler CreateHandler() =>
        new(new ImportDefinitionRegistry([_definition]), _dispatcher.Object);

    private static SubmittedImportRow<ImportEmployeeDto> Row(string importId = "r1") =>
        new(importId, new ImportEmployeeDto(
            "E-1001", "Dana", null, "Reyes", new EmailAddress("dana.reyes@example.com"),
            null, null, null, null, null));

    private Task<CSharpFunctionalExtensions.Result<Guid>> Handle(
        params SubmittedImportRow<ImportEmployeeDto>[] rows) =>
        CreateHandler().Handle(new ImportEmployeesCommand(rows), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsTheRunAgainstTheEmployeeImport()
    {
        // Arrange & Act
        var result = await Handle(Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.ImportType == EmployeeImportDefinition.ImportKey),
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
            new ImportEmployeesCommand([Row()], groupId), TestContext.Current.CancellationToken);

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
        captured.Rows.Should().AllSatisfy(r => r.Payload.Should().Contain("E-1001"));
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
        // Arrange
        var handler = new ImportEmployeesCommandHandler(new ImportDefinitionRegistry([]), _dispatcher.Object);

        // Act
        var result = await handler.Handle(
            new ImportEmployeesCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_RejectsAFileWithNoRows()
    {
        // Arrange & Act — on the pipeline, so it holds for any caller
        var result = new ImportEmployeesCommandValidator().Validate(new ImportEmployeesCommand([]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_RejectsARowWhoseDataIsInvalid()
    {
        // Arrange — a missing employee number is the DTO's own rule, and nothing else was checking it
        var bad = Row() with { Data = Row().Data with { EmployeeNumber = string.Empty } };

        // Act
        var result = new ImportEmployeesCommandValidator().Validate(new ImportEmployeesCommand([bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAWellFormedRow()
    {
        // Arrange & Act
        var result = new ImportEmployeesCommandValidator().Validate(new ImportEmployeesCommand([Row()]));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
