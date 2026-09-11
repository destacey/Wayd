using NodaTime;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.Common.Domain.Imports;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Domain.Tests.Sut.Imports;

public sealed class ImportProcessRowTests
{
    private static readonly Instant _attempted = Instant.FromUtc(2026, 9, 5, 9, 5, 0);
    private static readonly Guid _createdId = Guid.Parse("0b7e4c28-9f03-4a56-8d72-0e5a3c96b1d4");

    [Fact]
    public void Create_StartsPendingWithItsPayload()
    {
        // Arrange & Act
        var row = ImportProcessRow.Create("emp-1", 1, """{"employeeNumber":"E-1"}""");

        // Assert
        row.Status.Should().Be(ImportRowStatus.Pending);
        row.ImportId.Should().Be("emp-1");
        row.RowNumber.Should().Be(1);
        row.Payload.Should().NotBeNull();
        row.AttemptedOn.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_DropsThePayloadItNoLongerNeeds()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();

        // Act
        row.MarkSucceeded(_createdId, _attempted);

        // Assert — the created record is the better record; keeping a copy only widens the data held
        row.Status.Should().Be(ImportRowStatus.Succeeded);
        row.CreatedEntityId.Should().Be(_createdId);
        row.Payload.Should().BeNull();
        row.AttemptedOn.Should().Be(_attempted);
    }

    [Fact]
    public void MarkFailed_KeepsThePayloadSoARetryCanReExecuteIt()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();
        var payload = row.Payload;

        // Act
        row.MarkFailed("Manager number could not be resolved.", _attempted);

        // Assert
        row.Status.Should().Be(ImportRowStatus.Failed);
        row.Error.Should().Be("Manager number could not be resolved.");
        row.Payload.Should().Be(payload);
    }

    [Fact]
    public void MarkCancelled_AppliesOnlyToARowNeverAttempted()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();

        // Act
        row.MarkCancelled(_attempted);

        // Assert
        row.Status.Should().Be(ImportRowStatus.Cancelled);
    }

    [Fact]
    public void MarkCancelled_LeavesAnAppliedRowAlone()
    {
        // Arrange — a row that landed before the cancellation reached the runner
        var row = new ImportProcessRowFaker().Generate();
        row.MarkSucceeded(_createdId, _attempted);

        // Act
        row.MarkCancelled(_attempted);

        // Assert
        row.Status.Should().Be(ImportRowStatus.Succeeded);
    }

    [Fact]
    public void Reset_ReturnsAFailedRowToPending()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();
        row.MarkFailed("Transient.", _attempted);

        // Act
        var result = row.Reset();

        // Assert
        result.IsSuccess.Should().BeTrue();
        row.Status.Should().Be(ImportRowStatus.Pending);
        row.Error.Should().BeNull();
        row.AttemptedOn.Should().BeNull();
    }

    [Fact]
    public void Reset_RefusesARowThatAlreadySucceeded()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();
        row.MarkSucceeded(_createdId, _attempted);

        // Act — re-executing it would create the record a second time
        var result = row.Reset();

        // Assert
        result.IsFailure.Should().BeTrue();
        row.Status.Should().Be(ImportRowStatus.Succeeded);
    }

    [Fact]
    public void Reset_RefusesARowTheRetentionSweepHasEmptied()
    {
        // Arrange — failed, but past its retention window, so there is nothing left to run
        var row = new ImportProcessRowFaker().AsPurged().Generate();

        // Act
        var result = row.Reset();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retention");
        row.Status.Should().Be(ImportRowStatus.Failed);
    }

    [Fact]
    public void PurgePayload_ClearsTheDataButKeepsTheOutcome()
    {
        // Arrange
        var row = new ImportProcessRowFaker().Generate();
        row.MarkFailed("Unresolved reference.", _attempted);

        // Act
        row.PurgePayload();

        // Assert — what happened stays auditable; whose data it was does not
        row.Payload.Should().BeNull();
        row.Status.Should().Be(ImportRowStatus.Failed);
        row.Error.Should().Be("Unresolved reference.");
    }

    [Fact]
    public void MarkFailed_ClipsAMessageTooLongForTheColumn()
    {
        // Arrange — a pass can produce a long message, and losing the save to it would lose every row
        // outcome in the chunk, not just this explanation
        var row = new ImportProcessRowFaker().Generate();
        var error = new string('e', ImportProcessRow.MaxMessageLength + 500);

        // Act
        row.MarkFailed(error, _attempted);

        // Assert
        row.Error.Should().HaveLength(ImportProcessRow.MaxMessageLength);
        row.Status.Should().Be(ImportRowStatus.Failed);
    }

    [Fact]
    public void RecordPassCompleted_CountsThePassesDoneAndNeverGoesBack()
    {
        // Arrange
        var row = ImportProcessRow.Create("emp-1", 1, """{"employeeNumber":"E-1"}""");
        row.RecordPassCompleted(1);

        // Act — a later attempt reporting an earlier pass again
        row.RecordPassCompleted(0);

        // Assert
        row.CompletedPassCount.Should().Be(2);
    }

    [Fact]
    public void Reset_KeepsTheProgressARetryCarriesOnFrom()
    {
        // Arrange — created by the first pass, then rejected by the second
        var row = ImportProcessRow.Create("emp-1", 1, """{"employeeNumber":"E-1"}""");
        row.RecordCreatedEntity(_createdId);
        row.RecordPassCompleted(0);
        row.MarkFailed("Manager not found.", _attempted);

        // Act
        row.Reset();

        // Assert — the record the first pass saved is still there, so the retry must not make it again
        row.Status.Should().Be(ImportRowStatus.Pending);
        row.CompletedPassCount.Should().Be(1);
        row.CreatedEntityId.Should().Be(_createdId);
    }
}
