using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public sealed class ImportTeamMembershipsCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly TeamMembershipImportDefinition _definition;

    public ImportTeamMembershipsCommandHandlerTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));
        _definition = new TeamMembershipImportDefinition(_dbContext, clock, new ImportPayloadSerializer());

        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(Guid.CreateVersion7()));
    }

    public void Dispose() => _dbContext.Dispose();

    private ImportTeamMembershipsCommandHandler CreateHandler() =>
        new(new ImportDefinitionRegistry([_definition]), _dispatcher.Object);

    private static SubmittedImportRow<ImportTeamMembershipDto> Row(string importId = "r1") =>
        new(importId, new ImportTeamMembershipDto("TEAM", "ART", new LocalDate(2024, 6, 1), null));

    private Task<CSharpFunctionalExtensions.Result<Guid>> Handle(
        params SubmittedImportRow<ImportTeamMembershipDto>[] rows) =>
        CreateHandler().Handle(new ImportTeamMembershipsCommand(rows), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsTheRunAgainstTheTeamMembershipImport()
    {
        // Arrange & Act
        var result = await Handle(Row());

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c => c.ImportType == TeamMembershipImportDefinition.ImportKey),
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
        captured.Rows.Should().AllSatisfy(r => r.Payload.Should().Contain("TEAM"));
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
        var handler = new ImportTeamMembershipsCommandHandler(
            new ImportDefinitionRegistry([]), _dispatcher.Object);

        // Act
        var result = await handler.Handle(
            new ImportTeamMembershipsCommand([Row()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_RejectsAFileWithNoRows()
    {
        // Arrange & Act — on the pipeline, so it holds for any caller
        var result = new ImportTeamMembershipsCommandValidator()
            .Validate(new ImportTeamMembershipsCommand([]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_RejectsARowWhoseDataIsInvalid()
    {
        // Arrange — a missing child code is the DTO's own rule, and nothing else was checking it
        var bad = Row() with { Data = Row().Data with { ChildCode = string.Empty } };

        // Act
        var result = new ImportTeamMembershipsCommandValidator()
            .Validate(new ImportTeamMembershipsCommand([bad]));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsAWellFormedRow()
    {
        // Arrange & Act
        var result = new ImportTeamMembershipsCommandValidator()
            .Validate(new ImportTeamMembershipsCommand([Row()]));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
