using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class SubmitImportCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 5, 10, 0, 0);

    private readonly FakeImportDbContext _db = new();
    private readonly TestImportDefinition _definition = new(new TestSerializerService());
    private readonly Mock<IDispatcher> _dispatcher = new();

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

    [Fact]
    public async Task Handle_AppliesASmallFileInTheRequest()
    {
        // Arrange — the definition's inline threshold is the base default of 100

        // Act
        await Submit(Rows(3));

        // Assert — invoked, not queued, so the caller has an answer in the response
        _dispatcher.Verify(d => d.Send(It.IsAny<RunImportProcessCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Publish(It.IsAny<RunImportProcessCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_QueuesAFileTooLargeToApplyInline()
    {
        // Arrange & Act — past the inline threshold
        await Submit(Rows(101));

        // Assert
        _dispatcher.Verify(d => d.Publish(It.IsAny<RunImportProcessCommand>(), It.IsAny<CancellationToken>()), Times.Once);
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
}
