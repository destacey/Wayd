using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class GetImportProcessQueryHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly FakeWaydDbContext _waydDb = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<ICurrentPrincipal> _principal = new();

    public GetImportProcessQueryHandlerTests()
    {
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _db.Dispose();

    private GetImportProcessQueryHandler CreateHandler() =>
        new(_db, _waydDb, new ImportDefinitionRegistry([_definition]), _principal.Object);

    private ImportProcess AddRun(int rowCount)
    {
        var rows = Enumerable.Range(1, rowCount).Select(i =>
            ImportProcessRow.Create($"r{i}", i, _definition.SerializeRow(new TestImportRow($"Row {i}"))));

        var process = ImportProcess.Create(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(process);
        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<ImportProcessDto>> Get(Guid id) =>
        CreateHandler().Handle(new GetImportProcessQuery(id), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ReturnsTheRunWithWhatTheDefinitionKnowsAboutIt()
    {
        // Arrange — the counts alone cannot explain an all-or-nothing run that applied none of its rows
        _definition.AtomicityOverride = ImportAtomicity.Atomic;
        var process = AddRun(3);

        // Act
        var result = await Get(process.Id);

        // Assert
        result.Value.DisplayName.Should().Be(_definition.DisplayName);
        result.Value.Atomicity.Should().Be(ImportAtomicity.Atomic);
        result.Value.Status.Should().Be(ImportProcessStatus.Queued);
        result.Value.TotalRowCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ReportsWhatAResumeWouldPickUp()
    {
        // Arrange — one applied, one rejected, one never reached
        var process = AddRun(3);
        process.Start("trace-1", _now);
        process.RecordProgress(succeeded: 1, failed: 1, _now);

        // Act
        var result = await Get(process.Id);

        // Assert
        result.Value.UnappliedRowCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RefusesACallerWithoutThePermissionTheImportDeclares()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var process = AddRun(1);

        // Act
        var result = await Get(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForAnUnknownImport_Fails()
    {
        // Arrange & Act
        var result = await Get(Guid.CreateVersion7());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }
}
