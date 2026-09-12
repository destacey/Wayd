using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.Web.Api.Models.ProductManagement.Deployments;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Deployments;

/// <summary>
/// A deployment CSV row, and what it becomes. The row's own work is reading timestamps that carry their
/// offset and refusing the shapes an outcome cannot have.
/// </summary>
public sealed class ImportDeploymentRequestTests
{
    private readonly ImportDeploymentRequestValidator _validator = new();

    private static ImportDeploymentRequest Row(
        string startedAt = "2026-03-01T14:30:00Z",
        string? outcome = null,
        string? completedAt = null,
        string? rolledBackAt = null) =>
        new()
        {
            VersionId = Guid.CreateVersion7(),
            EnvironmentName = "Production",
            StartedAt = startedAt,
            Outcome = outcome,
            CompletedAt = completedAt,
            RolledBackAt = rolledBackAt,
        };

    [Fact]
    public void ToImportDeploymentDto_ReadsEachTimestampAsTheInstantItsOffsetDescribes()
    {
        // Arrange — the same instant written in two zones must read as one instant
        var request = Row(
            startedAt: "2026-03-01T14:30:00Z",
            outcome: "RolledBack",
            completedAt: "2026-03-01T09:45:00-05:00",
            rolledBackAt: "2026-03-01T16:00:00+01:00");

        // Act
        var dto = request.ToImportDeploymentDto();

        // Assert
        dto.StartedAt.Should().Be(Instant.FromUtc(2026, 3, 1, 14, 30));
        dto.CompletedAt.Should().Be(Instant.FromUtc(2026, 3, 1, 14, 45));
        dto.RolledBackAt.Should().Be(Instant.FromUtc(2026, 3, 1, 15, 0));
        dto.Outcome.Should().Be(ImportDeploymentOutcome.RolledBack);
    }

    [Fact]
    public void ToImportDeploymentDto_LeavesABlankOutcomeInFlight()
    {
        // Arrange
        var request = Row(outcome: " ");

        // Act
        var dto = request.ToImportDeploymentDto();

        // Assert
        dto.Outcome.Should().BeNull();
        dto.CompletedAt.Should().BeNull();
        dto.RolledBackAt.Should().BeNull();
    }

    [Fact]
    public void Validate_AcceptsARowStillInFlight()
    {
        // Arrange
        var request = Row();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("succeeded")]
    [InlineData("FAILED")]
    public void Validate_AcceptsAnOutcomeInAnyCase(string outcome)
    {
        // Arrange
        var request = Row(outcome: outcome, completedAt: "2026-03-01T15:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RefusesATimestampWithNoOffset()
    {
        // Arrange — read in the server's zone, a bare timestamp would shift every historical deployment
        // by whatever that zone happens to be
        var request = Row(startedAt: "2026-03-01T14:30:00");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.StartedAt);
    }

    [Fact]
    public void Validate_RefusesAnOutcomeWithNoCompletionTime()
    {
        // Arrange
        var request = Row(outcome: "Succeeded");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.CompletedAt);
    }

    [Fact]
    public void Validate_RefusesACompletionTimeOnARowStillInFlight()
    {
        // Arrange
        var request = Row(completedAt: "2026-03-01T15:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.CompletedAt);
    }

    [Fact]
    public void Validate_RefusesACompletionBeforeTheStart()
    {
        // Arrange
        var request = Row(startedAt: "2026-03-01T14:30:00Z", outcome: "Failed", completedAt: "2026-03-01T14:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.CompletedAt);
    }

    [Fact]
    public void Validate_RefusesARollbackWithNoRollbackTime()
    {
        // Arrange
        var request = Row(outcome: "RolledBack", completedAt: "2026-03-01T15:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.RolledBackAt);
    }

    [Fact]
    public void Validate_RefusesARollbackTimeOnAnyOtherOutcome()
    {
        // Arrange
        var request = Row(outcome: "Succeeded", completedAt: "2026-03-01T15:00:00Z", rolledBackAt: "2026-03-01T16:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.RolledBackAt);
    }

    [Fact]
    public void Validate_RefusesARollbackBeforeTheCompletion()
    {
        // Arrange
        var request = Row(outcome: "RolledBack", completedAt: "2026-03-01T15:00:00Z", rolledBackAt: "2026-03-01T14:45:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.RolledBackAt);
    }

    [Fact]
    public void Validate_RefusesAnOutcomeADeploymentCannotHave()
    {
        // Arrange
        var request = Row(outcome: "Released", completedAt: "2026-03-01T15:00:00Z");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Outcome);
    }

    [Fact]
    public void Validate_RefusesARowNamingBothAVersionAndAPackage()
    {
        // Arrange
        var request = Row();
        request.PackageId = Guid.CreateVersion7();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
