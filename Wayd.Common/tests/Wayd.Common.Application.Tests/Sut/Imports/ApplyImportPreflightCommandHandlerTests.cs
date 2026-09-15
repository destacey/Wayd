using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class ApplyImportPreflightCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 14, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<ICurrentPrincipal> _principal = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Guid _newRunId = Guid.CreateVersion7();

    public ApplyImportPreflightCommandHandlerTests()
    {
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Success(_newRunId));
    }

    public void Dispose() => _db.Dispose();

    private ApplyImportPreflightCommandHandler CreateHandler() =>
        new(_db, new ImportDefinitionRegistry([_definition]), _principal.Object, _dispatcher.Object);

    /// <summary>A finished preflight of three rows, the second of which it rejected.</summary>
    private ImportProcess FinishedPreflight(bool preflight = true)
    {
        // Stored out of order, so the test can see the submission restores the file's order.
        var rows = new[] { 3, 1, 2 }.Select(i =>
            ImportProcessRow.Create($"r{i}", i, _definition.SerializeRow(new TestImportRow($"Row {i}"))));

        var process = preflight
            ? ImportProcess.CreatePreflight(_definition.Key, "user-1", null, rows, _now)
            : ImportProcess.Create(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(process);

        process.Start("trace-1", _now);
        foreach (var row in process.Rows)
        {
            if (row.ImportId == "r2")
                row.MarkFailed("Rejected.", _now);
            else
                row.MarkPassedPreflight(warning: null, _now);
        }
        process.RecordProgress(succeeded: 2, failed: 1, _now);
        process.Complete(_now);

        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<Guid>> Apply(Guid id, Guid? groupId = null) =>
        CreateHandler().Handle(new ApplyImportPreflightCommand(id, groupId), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_SubmitsEveryStoredRowInFileOrderAsARealRun()
    {
        // Arrange
        var preflight = FinishedPreflight();
        var groupId = Guid.CreateVersion7();

        // Act
        var result = await Apply(preflight.Id, groupId);

        // Assert — rejected rows included, exactly as uploading the file again would
        result.Value.Should().Be(_newRunId);
        preflight.AppliedImportProcessId.Should().Be(_newRunId);
        _db.SaveChangesCallCount.Should().Be(1);
        _dispatcher.Verify(
            d => d.Send(
                It.Is<SubmitImportCommand>(c =>
                    c.ImportType == _definition.Key
                    && !c.ValidateOnly
                    && c.SubmissionGroupId == groupId
                    && c.Rows.Select(r => r.ImportId).SequenceEqual(new[] { "r1", "r2", "r3" })
                    && c.Rows.All(r => r.Payload.Contains("Row "))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LeavesNoLinkWhenTheSubmissionIsRefused()
    {
        // Arrange — the new run was never created, so there is nothing to point at
        var preflight = FinishedPreflight();
        _dispatcher
            .Setup(d => d.Send(It.IsAny<SubmitImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CSharpFunctionalExtensions.Result.Failure<Guid>("Too many rows."));

        // Act
        var result = await Apply(preflight.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        preflight.AppliedImportProcessId.Should().BeNull();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RefusesARunThatWasNotAPreflight()
    {
        // Arrange
        var run = FinishedPreflight(preflight: false);

        // Act
        var result = await Apply(run.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RefusesAPreflightThatHasNotFinished()
    {
        // Arrange
        var rows = new[] { ImportProcessRow.Create("r1", 1, _definition.SerializeRow(new TestImportRow("Row 1"))) };
        var preflight = ImportProcess.CreatePreflight(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(preflight);
        preflight.Start("trace-1", _now);

        // Act
        var result = await Apply(preflight.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Processing");
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RefusesAPreflightWhoseRowsThePurgeRemoved()
    {
        // Arrange — applying only the rows that survived would import part of the file
        var preflight = FinishedPreflight();
        preflight.Rows.Single(r => r.ImportId == "r1").PurgePayload();

        // Act
        var result = await Apply(preflight.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retention window");
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RefusesSomeoneWhoOnlyOverseesImports()
    {
        // Arrange — applying creates records, which oversight does not grant
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal
            .Setup(p => p.HasPermission(
                ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Imports),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var preflight = FinishedPreflight();

        // Act
        var result = await Apply(preflight.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_FailsWhenTheRunDoesNotExist()
    {
        // Arrange & Act
        var result = await Apply(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("was not found");
    }
}
