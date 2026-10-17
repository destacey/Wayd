using FluentAssertions;
using Wayd.Work.Application.WorkTeams.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamCadenceMetricsQueryValidatorTests
{
    private readonly GetTeamCadenceMetricsQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    [InlineData(12)]
    public void Validate_NoWindowOrOneInRange_IsValid(int? periodWindow)
    {
        // Act
        var result = _validator.Validate(new GetTeamCadenceMetricsQuery(Guid.NewGuid(), periodWindow));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(13)]
    public void Validate_WindowOutOfRange_IsInvalid(int periodWindow)
    {
        // Act
        var result = _validator.Validate(new GetTeamCadenceMetricsQuery(Guid.NewGuid(), periodWindow));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GetTeamCadenceMetricsQuery.PeriodWindow));
    }
}
