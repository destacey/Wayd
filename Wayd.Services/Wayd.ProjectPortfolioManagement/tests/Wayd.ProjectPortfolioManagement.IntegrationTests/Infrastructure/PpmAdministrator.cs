using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Common;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// The caller a PPM handler test acts as: an employee holding the PPM administrator grant, so authorization never
/// decides the outcome and only the record's own state can refuse.
/// </summary>
public static class PpmAdministrator
{
    /// <summary>A principal linked to <paramref name="employeeId"/> who holds the PPM administrator grant.</summary>
    public static ICurrentPrincipal Principal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal
            .Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
        principal
            .Setup(p => p.HasPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return principal.Object;
    }

    /// <summary>The signed-in account behind <see cref="Principal"/>.</summary>
    public static ICurrentUser User()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");
        return currentUser.Object;
    }
}
