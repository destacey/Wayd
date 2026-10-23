using System.Reflection;
using FluentAssertions;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Web.Api.Controllers.Organizations;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Organizations;

public sealed class TeamTypesControllerTests
{
    [Fact]
    public void GetList_RequiresTheViewTeamsPermission()
    {
        // Arrange
        var expected = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Teams);

        // Act
        var policy = typeof(TeamTypesController)
            .GetMethod(nameof(TeamTypesController.GetList))!
            .GetCustomAttribute<MustHavePermissionAttribute>()!
            .Policy;

        // Assert
        policy.Should().Be(expected);
    }
}
