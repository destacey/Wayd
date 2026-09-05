using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class CancelImportProcessCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<ICurrentPrincipal> _principal = new();

    public CancelImportProcessCommandHandlerTests()
    {
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _db.Dispose();

    private CancelImportProcessCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        return new CancelImportProcessCommandHandler(
            _db,
            new ImportDefinitionRegistry([_definition]),
            _principal.Object,
            clock.Object,
            NullLogger<CancelImportProcessCommandHandler>.Instance);
    }

    private ImportProcess QueueRun(int rowCount = 2)
    {
        var rows = Enumerable.Range(1, rowCount).Select(i =>
            ImportProcessRow.Create($"r{i}", i, _definition.SerializeRow(new TestImportRow($"Row {i}"))));

        var process = ImportProcess.Create(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(process);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result> Cancel(Guid id) =>
        CreateHandler().Handle(new CancelImportProcessCommand(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_EndsARunNoWorkerHasClaimed()
    {
        // Arrange — nothing has been applied, and there is no worker to act on a request
        var process = QueueRun();

        // Act
        var result = await Cancel(process.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelled);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Cancelled));
    }

    [Fact]
    public async Task Handle_OnlyAsksARunningImportToStop()
    {
        // Arrange — the worker owns the run; ending it here would strand records against Pending rows
        var process = QueueRun();
        process.Start("trace-1", _now);

        // Act
        var result = await Cancel(process.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Cancelling);
        process.Rows.Should().AllSatisfy(r => r.Status.Should().Be(ImportRowStatus.Pending));
    }

    [Fact]
    public async Task Handle_RefusesARunThatAlreadyFinished()
    {
        // Arrange
        var process = QueueRun();
        process.Start("trace-1", _now);
        process.Complete(_now);

        // Act
        var result = await Cancel(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RefusesACallerWithoutThePermissionTheImportDeclares()
    {
        // Arrange — there is no blanket imports permission; the run's own definition names the gate
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var process = QueueRun();

        // Act
        var result = await Cancel(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        process.Status.Should().Be(ImportProcessStatus.Queued);
    }

    [Fact]
    public async Task Handle_ForAnUnknownImport_Fails()
    {
        // Arrange & Act
        var result = await Cancel(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }
}
