using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

/// <summary>
/// The DI container auto-registers <see cref="ITransientService"/> implementations
/// by their first interface (see Common.ConfigureServices.AddServices). This pins
/// that <see cref="OidcProviderDefaultRoleChecker"/> resolves to its specific
/// contract rather than the bare marker — a wiring guarantee that ordinary unit
/// tests (which inject the mock directly) wouldn't catch.
/// </summary>
public sealed class OidcProviderDefaultRoleCheckerRegistrationTests
{
    [Fact]
    public void Implementation_ResolvesToItsSpecificContract_NotTheMarker()
    {
        // Arrange
        var type = typeof(OidcProviderDefaultRoleChecker);

        // Act — mirrors the scanner's selection: the first declared interface.
        var registeredAgainst = type.GetInterfaces().FirstOrDefault();

        // Assert
        registeredAgainst.Should().Be<IOidcProviderDefaultRoleChecker>();
        typeof(IOidcProviderDefaultRoleChecker).Should().BeAssignableTo<ITransientService>();
    }
}
