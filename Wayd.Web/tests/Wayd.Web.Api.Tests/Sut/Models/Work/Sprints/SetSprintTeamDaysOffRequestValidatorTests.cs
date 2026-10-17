using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Web.Api.Models.Work.Sprints;

namespace Wayd.Web.Api.Tests.Sut.Models.Work.Sprints;

public sealed class SetSprintTeamDaysOffRequestValidatorTests
{
    private static readonly LocalDate Day = new(2026, 10, 1);

    private readonly SetSprintTeamDaysOffRequestValidator _validator = new();

    [Fact]
    public void FullAndPartDays_Pass()
    {
        // Arrange
        var request = new SetSprintTeamDaysOffRequest
        {
            TeamDaysOff = [new() { Date = Day }, new() { Date = Day.PlusDays(1), PercentOff = 50 }],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ANullDay_Fails()
    {
        // Arrange — the serializer lets a null element through, and the command would dereference it
        var request = new SetSprintTeamDaysOffRequest { TeamDaysOff = [new() { Date = Day }, null!] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("TeamDaysOff[1]")
            .WithErrorMessage("Each team day off must be a day with a date.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void APercentOutsideOneTo100_Fails(int percentOff)
    {
        // Arrange
        var request = new SetSprintTeamDaysOffRequest { TeamDaysOff = [new() { Date = Day, PercentOff = percentOff }] };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor("TeamDaysOff[0]")
            .WithErrorMessage("A team day off must be off for 1 to 100 percent of the day.");
    }

    [Fact]
    public void ADayListedTwice_Fails()
    {
        // Arrange
        var request = new SetSprintTeamDaysOffRequest
        {
            TeamDaysOff = [new() { Date = Day }, new() { Date = Day, PercentOff = 50 }],
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TeamDaysOff)
            .WithErrorMessage("Each team day off can be listed only once.");
    }
}
