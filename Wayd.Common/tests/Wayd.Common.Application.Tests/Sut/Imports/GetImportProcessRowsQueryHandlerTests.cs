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

public sealed class GetImportProcessRowsQueryHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<ICurrentPrincipal> _principal = new();

    public GetImportProcessRowsQueryHandlerTests()
    {
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _db.Dispose();

    private GetImportProcessRowsQueryHandler CreateHandler() =>
        new(_db, new ImportDefinitionRegistry([_definition]), _principal.Object);

    /// <summary>Ten rows: the first three rejected, the rest applied.</summary>
    private ImportProcess AddPartlyRejectedRun()
    {
        var rows = Enumerable.Range(1, 10).Select(i =>
            ImportProcessRow.Create($"r{i}", i, _definition.SerializeRow(new TestImportRow($"Row {i}"))));

        var process = ImportProcess.Create(_definition.Key, "user-1", null, rows, _now);
        _db.AddImportProcess(process);
        process.Start("trace-1", _now);

        foreach (var row in process.Rows)
        {
            if (row.RowNumber <= 3)
                row.MarkFailed($"Row {row.RowNumber} was rejected.", _now);
            else
                row.MarkSucceeded(Guid.CreateVersion7(), _now);
        }

        return process;
    }

    private Task<CSharpFunctionalExtensions.Result<ImportProcessRowPageDto>> Get(
        Guid id, ImportRowStatus? status = null, int pageNumber = 1, int pageSize = 50) =>
        CreateHandler().Handle(
            new GetImportProcessRowsQuery(id, status, pageNumber, pageSize), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ReturnsEveryRowInFilePositionOrder()
    {
        // Arrange
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id);

        // Assert
        result.Value.TotalCount.Should().Be(10);
        result.Value.Rows.Select(r => r.RowNumber).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Handle_NarrowsToOneStatus()
    {
        // Arrange — the answer a person wants is almost always the rejected rows
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id, ImportRowStatus.Failed);

        // Assert
        result.Value.TotalCount.Should().Be(3);
        result.Value.Rows.Should().AllSatisfy(r => r.Error.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_PagesWithoutLosingTheTotal()
    {
        // Arrange — a UI needs the total to page at all, and a run can hold fifty thousand rows
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id, pageNumber: 2, pageSize: 4);

        // Assert
        result.Value.TotalCount.Should().Be(10);
        result.Value.Rows.Select(r => r.RowNumber).Should().Equal(5, 6, 7, 8);
    }

    [Fact]
    public async Task Handle_ClampsAPageSizeNobodyShouldAskFor()
    {
        // Arrange
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id, pageNumber: 0, pageSize: 100_000);

        // Assert
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(500);
    }

    [Fact]
    public async Task Handle_RefusesACallerWithoutThePermissionTheImportDeclares()
    {
        // Arrange
        _principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _principal.Setup(p => p.HoldsPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LinksEachRowToTheRecordItCreated()
    {
        // Arrange
        var process = AddPartlyRejectedRun();
        var applied = process.Rows.Single(r => r.RowNumber == 4);
        var link = new ImportRecordLink(ImportRecordType.Project, "APOLLO", "Apollo");
        _definition.Links[applied.CreatedEntityId!.Value] = link;

        // Act
        var result = await Get(process.Id);

        // Assert
        result.Value.Rows.Single(r => r.RowNumber == 4).CreatedRecord.Should().Be(link);
    }

    [Fact]
    public async Task Handle_LeavesTheLinkEmptyForARecordThatNoLongerExists()
    {
        // Arrange — the lookup leaves out a deleted record, and the row still reports the id it created
        var process = AddPartlyRejectedRun();
        var stillThere = process.Rows.Single(r => r.RowNumber == 4);
        var gone = process.Rows.Single(r => r.RowNumber == 5);
        _definition.Links[stillThere.CreatedEntityId!.Value] = new ImportRecordLink(ImportRecordType.Risk, "42", "Vendor delay");

        // Act
        var result = await Get(process.Id, ImportRowStatus.Succeeded);

        // Assert
        _definition.DescribeCalls.Should().ContainSingle().Which.Should().Contain(gone.CreatedEntityId!.Value);
        result.Value.Rows.Single(r => r.RowNumber == 4).CreatedRecord.Should().NotBeNull();
        var goneRow = result.Value.Rows.Single(r => r.RowNumber == 5);
        goneRow.CreatedEntityId.Should().Be(gone.CreatedEntityId);
        goneRow.CreatedRecord.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AsksOnlyAboutThePageItReturns()
    {
        // Arrange
        var process = AddPartlyRejectedRun();
        var onPage = process.Rows.Where(r => r.RowNumber is 5 or 6).Select(r => r.CreatedEntityId!.Value);

        // Act
        await Get(process.Id, pageNumber: 3, pageSize: 2);

        // Assert
        _definition.DescribeCalls.Should().ContainSingle().Which.Should().BeEquivalentTo(onPage);
    }

    [Fact]
    public async Task Handle_MakesNoLookupWhenNoRowOnThePageCreatedAnything()
    {
        // Arrange
        var process = AddPartlyRejectedRun();

        // Act
        var result = await Get(process.Id, ImportRowStatus.Failed);

        // Assert
        result.Value.Rows.Should().AllSatisfy(r => r.CreatedRecord.Should().BeNull());
        _definition.DescribeCalls.Should().BeEmpty();
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
