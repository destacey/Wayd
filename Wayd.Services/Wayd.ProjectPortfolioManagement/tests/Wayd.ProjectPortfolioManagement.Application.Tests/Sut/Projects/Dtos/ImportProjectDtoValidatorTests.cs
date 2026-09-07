using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Dtos;

/// <summary>
/// Which transition dates a row may carry, and which it must. A date for a state the project never
/// reached is rejected rather than dropped: accepting it would import a project whose history disagrees
/// with the file that produced it, and the file's author would never hear about it.
/// </summary>
public sealed class ImportProjectDtoValidatorTests
{
    private static readonly LocalDate _created = new(2024, 3, 4);
    private static readonly LocalDate _activated = new(2024, 7, 8);
    private static readonly LocalDate _closed = new(2025, 5, 6);

    private readonly ImportProjectDtoValidator _sut = new();

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    public void Validate_RejectsAProjectPastProposedWithNoActivationDate(ProjectStatus status)
    {
        // Arrange
        var row = Row(status) with { ActivatedOn = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportProjectDto.ActivatedOn));
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    public void Validate_RejectsAnActivationDateOnAProjectThatNeverActivated(ProjectStatus status)
    {
        // Arrange
        var row = Row(status) with { ActivatedOn = _activated };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsACanceledProjectWithNoActivationDate()
    {
        // Arrange — canceled before it ever started
        var row = Row(ProjectStatus.Canceled) with { ActivatedOn = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AcceptsACanceledProjectWithAnActivationDate()
    {
        // Arrange — the other half of the same rule: canceled mid-flight. Canceled is the one status the
        // row alone cannot settle, which is why the date is optional here and nowhere else.
        var row = Row(ProjectStatus.Canceled) with { ActivatedOn = _activated };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Canceled)]
    public void Validate_RejectsAClosedProjectWithNoClosingDate(ProjectStatus status)
    {
        // Arrange
        var row = Row(status) with { ClosedOn = null };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ImportProjectDto.ClosedOn));
    }

    [Theory]
    [InlineData(ProjectStatus.Proposed)]
    [InlineData(ProjectStatus.Approved)]
    [InlineData(ProjectStatus.Active)]
    public void Validate_RejectsAClosingDateOnAProjectThatIsStillOpen(ProjectStatus status)
    {
        // Arrange
        var row = Row(status) with { ClosedOn = _closed };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsAnActivationBeforeTheProjectWasCreated()
    {
        // Arrange
        var row = Row(ProjectStatus.Active) with { ActivatedOn = _created.PlusDays(-1) };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsAClosureBeforeTheActivationItFollows()
    {
        // Arrange
        var row = Row(ProjectStatus.Completed) with { ClosedOn = _activated.PlusDays(-1) };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsAClosureBeforeCreationOnAProjectThatNeverActivated()
    {
        // Arrange — with no activation to measure against, creation is the floor
        var row = Row(ProjectStatus.Canceled) with { ActivatedOn = null, ClosedOn = _created.PlusDays(-1) };

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsAProjectThatRanItsWholeCourseInOrder()
    {
        // Arrange
        var row = Row(ProjectStatus.Completed);

        // Act
        var result = _sut.Validate(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    /// <summary>A row carrying the dates its status calls for, so each test changes only what it is about.</summary>
    private static ImportProjectDto Row(ProjectStatus status) =>
        new(
            "Apollo",
            "Apollo description",
            new ProjectKey("APOLLO"),
            status,
            Guid.CreateVersion7(),
            null,
            1,
            status is ProjectStatus.Approved ? Guid.CreateVersion7() : null,
            null,
            null,
            new LocalDate(2024, 7, 1),
            new LocalDate(2025, 6, 30),
            _created,
            status is ProjectStatus.Active or ProjectStatus.Completed or ProjectStatus.Canceled ? _activated : null,
            status is ProjectStatus.Completed or ProjectStatus.Canceled ? _closed : null,
            [],
            [],
            [],
            [],
            []);
}
