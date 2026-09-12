using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Planning.PlanningIntervals;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.PlanningIntervals;

/// <summary>
/// A planning interval CSV row, and what it becomes. The row's own work is reading the roster out of a
/// semicolon-separated cell and handing the dates over as dates.
/// </summary>
public sealed class ImportPlanningIntervalRequestTests
{
    private readonly ImportPlanningIntervalRequestValidator _validator = new();

    private static ImportPlanningIntervalRequest Row(
        string name = "PI 2026.1",
        DateOnly? start = null,
        DateOnly? end = null,
        int iterationWeeks = 2,
        string? iterationPrefix = "PI26.1-",
        string? teamIds = null) =>
        new()
        {
            ImportId = "r1",
            Name = name,
            Description = "The first interval of the year",
            Start = start ?? new DateOnly(2026, 1, 5),
            End = end ?? new DateOnly(2026, 2, 15),
            IterationWeeks = iterationWeeks,
            IterationPrefix = iterationPrefix,
            TeamIds = teamIds,
        };

    [Fact]
    public void ToImportPlanningIntervalDto_HandsTheDatesOverAsDates()
    {
        // Arrange — a DateOnly column reaches the domain's LocalDate with nothing inventing a time of day
        var request = Row(start: new DateOnly(2026, 1, 5), end: new DateOnly(2026, 2, 15));

        // Act
        var dto = request.ToImportPlanningIntervalDto();

        // Assert
        dto.Start.Should().Be(new LocalDate(2026, 1, 5));
        dto.End.Should().Be(new LocalDate(2026, 2, 15));
    }

    [Fact]
    public void ToImportPlanningIntervalDto_ReadsTheRosterFromTheSemicolonSeparatedCell()
    {
        // Arrange
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        // Act — whitespace around a value is the common hand-authored shape
        var dto = Row(teamIds: $" {first} ; {second} ").ToImportPlanningIntervalDto();

        // Assert
        dto.TeamIds.Should().Equal(first, second);
    }

    [Fact]
    public void ToImportPlanningIntervalDto_ReadsABlankRosterAsNoTeams()
    {
        // Arrange & Act — this import only creates, so a blank cell is an interval with no teams
        var dto = Row(teamIds: null).ToImportPlanningIntervalDto();

        // Assert
        dto.TeamIds.Should().BeEmpty();
    }

    [Fact]
    public void Validator_RejectsARowWithNoStartDate()
    {
        // Arrange
        var request = Row();
        request.Start = null;

        // Act & Assert
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(p => p.Start);
    }

    [Fact]
    public void Validator_RejectsAnEndBeforeTheStart()
    {
        // Arrange & Act
        var result = _validator.TestValidate(
            Row(start: new DateOnly(2026, 2, 15), end: new DateOnly(2026, 1, 5)));

        // Assert
        result.ShouldHaveValidationErrorFor(p => p.End)
            .WithErrorMessage("End date must be on or after the start date.");
    }

    [Fact]
    public void Validator_AcceptsASingleDayInterval()
    {
        // Arrange & Act — the range is inclusive, so start and end may be the same day
        var day = new DateOnly(2026, 1, 5);

        // Assert
        _validator.TestValidate(Row(start: day, end: day)).ShouldNotHaveValidationErrorFor(p => p.End);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validator_RejectsACadenceThatCannotGenerateIterations(int iterationWeeks)
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(iterationWeeks: iterationWeeks))
            .ShouldHaveValidationErrorFor(p => p.IterationWeeks);
    }

    [Fact]
    public void Validator_RejectsARosterThatIsNotAListOfIds()
    {
        // Arrange & Act
        var result = _validator.TestValidate(Row(teamIds: $"{Guid.CreateVersion7()};not-a-guid"));

        // Assert — reported rather than dropped, since a dropped cell silently imports a smaller roster
        result.ShouldHaveValidationErrorFor(p => p.TeamIds)
            .WithErrorMessage("TeamIds must be a semicolon-separated list of ids.");
    }

    [Fact]
    public void Validator_RejectsAnIterationPrefixThatWouldOverflowAnIterationName()
    {
        // Arrange & Act & Assert — an iteration name is the prefix plus a number, capped at 128
        _validator.TestValidate(Row(iterationPrefix: new string('x', 33)))
            .ShouldHaveValidationErrorFor(p => p.IterationPrefix);
    }

    [Fact]
    public void Validator_AcceptsACompleteRow()
    {
        // Arrange & Act & Assert
        _validator.TestValidate(Row(teamIds: Guid.CreateVersion7().ToString()))
            .ShouldNotHaveAnyValidationErrors();
    }
}
