using FluentAssertions;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetMyFollowsQueryValidatorTests
{
    private readonly GetMyFollowsQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptSeveralOwnerTypes()
    {
        // Arrange
        var query = new GetMyFollowsQuery(new PageRequest(), ["ppm.project", "ppm.program"]);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldReject_MoreOwnerTypesThanTheLimit()
    {
        // Arrange
        var ownerTypes = Enumerable.Range(0, GetMyFollowsQueryValidator.MaxOwnerTypes + 1).Select(i => $"test.type-{i}").ToList();
        var query = new GetMyFollowsQuery(new PageRequest(), ownerTypes);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetMyFollowsQuery.OwnerTypes));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ShouldReject_ABlankOwnerType(string? ownerType)
    {
        // Arrange
        var query = new GetMyFollowsQuery(new PageRequest(), ["ppm.project", ownerType!]);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldReject_AnOwnerTypeLongerThanAKey()
    {
        // Arrange
        var query = new GetMyFollowsQuery(new PageRequest(), [new string('a', FollowOwnerDescriptor.KeyMaxLength + 1)]);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
