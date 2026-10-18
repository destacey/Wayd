using FluentAssertions;
using Wayd.Web.Api.Models.ProductManagement.Releases;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Releases;

/// <summary>
/// A patch that removes a list leaves it null, so the validator must refuse it rather than throw.
/// </summary>
public sealed class SetReleaseContentsRequestValidatorTests
{
    private readonly SetReleaseContentsRequestValidator _validator = new();

    [Fact]
    public void Validate_RefusesANullList()
    {
        // Arrange
        var request = new SetReleaseContentsRequest { VersionIds = null!, PackageIds = null! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain([nameof(SetReleaseContentsRequest.VersionIds), nameof(SetReleaseContentsRequest.PackageIds)]);
    }

    [Fact]
    public void Validate_RefusesAnEmptyId()
    {
        // Arrange
        var request = new SetReleaseContentsRequest { VersionIds = [Guid.Empty], PackageIds = [] };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsEmptyLists()
    {
        // Arrange
        var request = new SetReleaseContentsRequest { VersionIds = [], PackageIds = [] };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
