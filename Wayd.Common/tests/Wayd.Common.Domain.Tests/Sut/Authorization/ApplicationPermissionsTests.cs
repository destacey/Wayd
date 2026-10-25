using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Domain.Tests.Sut.Authorization;

/// <summary>
/// Pins where permissions sit in the catalogue. <see cref="ApplicationPermissions.Admin"/> is the set the database
/// seeder gives the Admin role at every startup, so a permission there is one every admin holds.
/// </summary>
public sealed class ApplicationPermissionsTests
{
    [Fact]
    public void RateLimitsElevated_IsGivenToAdmins()
    {
        // Arrange
        const string name = "Permissions.RateLimits.Elevated";

        // Act
        var admin = ApplicationPermissions.Admin.Select(p => p.Name);
        var root = ApplicationPermissions.Root.Select(p => p.Name);

        // Assert
        admin.Should().Contain(name);
        root.Should().NotContain(name);
    }
}
