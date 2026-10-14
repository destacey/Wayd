using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Infrastructure;

/// <summary>A caller for the time away tests: who they are, and whether they administer time away.</summary>
internal static class TestPrincipal
{
    /// <summary>A caller linked to <paramref name="employeeId"/>, or to no employee when it is null.</summary>
    public static ICurrentPrincipal LinkedTo(Guid? employeeId, bool timeAwayAdministrator = false)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        principal.Setup(p => p.HasPermission(TimeAwayAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(timeAwayAdministrator);
        return principal.Object;
    }
}
