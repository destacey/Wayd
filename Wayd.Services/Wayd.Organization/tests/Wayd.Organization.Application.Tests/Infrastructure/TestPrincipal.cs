using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Infrastructure;

/// <summary>A caller for the time away tests: who they are, whether they administer time away, and whether their token is read-only.</summary>
internal static class TestPrincipal
{
    /// <summary>A caller linked to <paramref name="employeeId"/>, or to no employee when it is null.</summary>
    public static ICurrentPrincipal LinkedTo(Guid? employeeId, bool timeAwayAdministrator = false, bool readOnly = false)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        principal.Setup(p => p.HasPermission(TimeAwayAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(timeAwayAdministrator && !readOnly);
        principal.Setup(p => p.HoldsPermission(TimeAwayAuthorization.AdministratorPermission, It.IsAny<CancellationToken>())).ReturnsAsync(timeAwayAdministrator);
        principal.Setup(p => p.IsReadOnly).Returns(readOnly);
        return principal.Object;
    }
}
