using FluentAssertions;
using NodaTime;
using Wayd.ProductManagement.Application.Releases.Queries;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Queries;

public sealed class GetTeamsUpcomingReleasesQueryValidatorTests
{
    private readonly GetTeamsUpcomingReleasesQueryValidator _sut = new();

    private static GetTeamsUpcomingReleasesQuery Query(int days) =>
        new([Guid.CreateVersion7()], new LocalDate(2026, 4, 1), days, IncludeReleases: true, IncludeVersions: true);

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(GetTeamsUpcomingReleasesQueryValidator.MaxDays)]
    public void Validate_ShouldAcceptAWindowWithinTheLimit(int days)
    {
        // Arrange
        var query = Query(days);

        // Act
        var result = _sut.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(GetTeamsUpcomingReleasesQueryValidator.MaxDays + 1)]
    public void Validate_ShouldRejectAWindowOutsideTheLimit(int days)
    {
        // Arrange
        var query = Query(days);

        // Act
        var result = _sut.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
