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

public sealed class SubmitImportCommandHandlerTests : IDisposable
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new ImportPayloadSerializer());
    private readonly Mock<IDispatcher> _dispatcher = new();

    public void Dispose() => _db.Dispose();

    private SubmitImportCommandHandler CreateHandler()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(_now);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("user-1");

        return new SubmitImportCommandHandler(
            _db,
            new ImportDefinitionRegistry([_definition]),
            currentUser.Object,
            clock.Object,
            _dispatcher.Object,
            NullLogger<SubmitImportCommandHandler>.Instance);
    }

    private List<SubmittedImportRow> Rows(int count, string? importIdPrefix = "r") =>
        [.. Enumerable.Range(1, count).Select(i => new SubmittedImportRow(
            importIdPrefix is null ? null : $"{importIdPrefix}{i}",
            _definition.SerializeRow(new TestImportRow($"Row {i}"))))];

    private Task<CSharpFunctionalExtensions.Result<Guid>> Submit(List<SubmittedImportRow> rows) =>
        CreateHandler().Handle(
            new SubmitImportCommand("test-import", rows), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_RecordsTheRunAndReturnsItsId()
    {
        // Arrange & Act
        var result = await Submit(Rows(3));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var process = _db.ImportProcesses.Single();
        process.Id.Should().Be(result.Value);
        process.ImportType.Should().Be("test-import");
        process.SubmittedByUserId.Should().Be("user-1");
        process.TotalRowCount.Should().Be(3);
        process.Status.Should().Be(ImportProcessStatus.Queued);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5_000)]
    public async Task Handle_QueuesTheRunWhateverItsSize(int rowCount)
    {
        // Arrange & Act
        var result = await Submit(Rows(rowCount));

        // Assert — never applied in the request, and attributed to the submitter
        _dispatcher.Verify(
            d => d.Publish(
                It.Is<RunImportProcessCommand>(c => c.ImportProcessId == result.Value),
                "user-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<RunImportProcessCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsAFileOverTheRowCapBeforePersistingAnything()
    {
        // Arrange — MaxRows is the base default of 50,000
        var rows = Rows(50_001);

        // Act
        var result = await Submit(rows);

        // Assert — nothing recorded, nothing queued
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("50,001");
        _db.ImportProcesses.Should().BeEmpty();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_RejectsDuplicateImportIdsAndNamesTheOffendingKey()
    {
        // Arrange
        var rows = Rows(2);
        rows.Add(new SubmittedImportRow("r1", _definition.SerializeRow(new TestImportRow("Duplicate"))));

        // Act
        var result = await Submit(rows);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("r1");
        _db.ImportProcesses.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FallsBackToTheRowPositionWhenNoImportIdWasSupplied()
    {
        // Arrange — a hand-authored file with no ImportId column

        // Act
        await Submit(Rows(3, importIdPrefix: null));

        // Assert
        _db.ImportProcesses.Single().Rows.Select(r => r.ImportId).Should().Equal("1", "2", "3");
    }

    [Fact]
    public async Task Handle_RejectsAnEmptyFile()
    {
        // Arrange & Act
        var result = await Submit([]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no rows");
    }

    [Fact]
    public async Task Handle_RejectsAnImportTypeThatIsNotRegistered()
    {
        // Arrange & Act
        var result = await CreateHandler().Handle(
            new SubmitImportCommand("not-a-thing", Rows(1)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not-a-thing");
    }

    [Fact]
    public async Task Handle_CarriesTheSubmissionGroupSoRelatedFilesReadAsOneEntry()
    {
        // Arrange — a seed run submitting several files together
        var groupId = Guid.CreateVersion7();

        // Act
        await CreateHandler().Handle(
            new SubmitImportCommand("test-import", Rows(2), groupId), TestContext.Current.CancellationToken);

        // Assert
        _db.ImportProcesses.Single().SubmissionGroupId.Should().Be(groupId);
    }

    [Fact]
    public async Task Handle_RejectsAnImportIdLongerThanTheColumnHolds()
    {
        // Arrange — otherwise this surfaces at SaveChanges as a 500 naming a column
        var importId = new string('k', ImportProcessRow.MaxImportIdLength + 1);

        // Act
        var result = await Submit(
            [new SubmittedImportRow(importId, _definition.SerializeRow(new TestImportRow("Too long")))]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(ImportProcessRow.MaxImportIdLength.ToString());
        _db.SaveChangesCallCount.Should().Be(0);
    }
}
