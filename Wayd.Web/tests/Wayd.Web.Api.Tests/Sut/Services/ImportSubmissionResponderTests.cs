using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NodaTime;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Services;

public sealed class ImportSubmissionResponderTests
{
    private static readonly Guid _importProcessId = Guid.CreateVersion7();

    private readonly Mock<IDispatcher> _dispatcher = new();

    private ImportSubmissionResponder CreateResponder(TimeSpan budget) =>
        new(_dispatcher.Object, new ImportResponseTiming(budget, PollInterval: TimeSpan.FromMilliseconds(10)));

    private static ControllerBase CreateController() =>
        new StubController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static ImportProcessDto Run(ImportProcessStatus status) => new(
        _importProcessId, "test-import", "Test Import", ImportAtomicity.Atomic, status, null, "user-1", "User One",
        Instant.FromUtc(2026, 9, 10, 9, 0), null, null, null,
        TotalRowCount: 25, SucceededRowCount: 0, FailedRowCount: status == ImportProcessStatus.Failed ? 25 : 0,
        Error: null, CanManage: true);

    private void PollReturns(params ImportProcessStatus[] statuses)
    {
        var sequence = _dispatcher.SetupSequence(d => d.Send(It.IsAny<GetImportProcessQuery>(), It.IsAny<CancellationToken>()));
        foreach (var status in statuses)
            sequence = sequence.ReturnsAsync(Result.Success(Run(status)));
    }

    [Fact]
    public async Task Respond_AnswersWithTheOutcomeWhenTheRunHasAlreadyFinished()
    {
        // Arrange — a small file that failed completely: the case that used to read as "submitted"
        PollReturns(ImportProcessStatus.Failed);

        // Act
        var response = await CreateResponder(TimeSpan.FromSeconds(5))
            .Respond(CreateController(), _importProcessId, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ImportProcessDto>().Which.Status.Should().Be(ImportProcessStatus.Failed);
    }

    [Fact]
    public async Task Respond_KeepsWaitingWhileTheRunIsInFlightWithinTheBudget()
    {
        // Arrange
        PollReturns(ImportProcessStatus.Queued, ImportProcessStatus.Processing, ImportProcessStatus.Succeeded);

        // Act
        var response = await CreateResponder(TimeSpan.FromSeconds(5))
            .Respond(CreateController(), _importProcessId, TestContext.Current.CancellationToken);

        // Assert
        response.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ImportProcessDto>().Which.Status.Should().Be(ImportProcessStatus.Succeeded);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetImportProcessQuery>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Respond_HandsBackTheRunInFlightOnceTheBudgetIsSpent()
    {
        // Arrange — a budget too short for a second poll
        PollReturns(ImportProcessStatus.Processing);

        // Act
        var response = await CreateResponder(TimeSpan.Zero)
            .Respond(CreateController(), _importProcessId, TestContext.Current.CancellationToken);

        // Assert — the same body either way, and a location to follow it at
        var accepted = response.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Location.Should().Be($"/api/imports/{_importProcessId}");
        accepted.Value.Should().BeOfType<ImportProcessDto>().Which.Status.Should().Be(ImportProcessStatus.Processing);
    }

    [Fact]
    public async Task Respond_ReturnsBadRequestWhenTheRunCannotBeRead()
    {
        // Arrange
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetImportProcessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ImportProcessDto>("Import process was not found."));

        // Act
        var response = await CreateResponder(TimeSpan.FromSeconds(5))
            .Respond(CreateController(), _importProcessId, TestContext.Current.CancellationToken);

        // Assert
        response.Should().BeOfType<BadRequestObjectResult>();
    }

    private sealed class StubController : ControllerBase;
}
