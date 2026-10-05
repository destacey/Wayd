using System.Net;
using Wayd.Common.Domain.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Holds a route's id-or-key value to naming no record, rather than failing the request, when it is neither a
/// Guid nor in the key's format. Agents calling the API through MCP guess keys, so a malformed one is a routine
/// request, not a server error.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class IdOrKeyRouteTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Theory]
    [InlineData("/api/ppm/projects/NO-SUCH-PROJECT-KEY")]
    [InlineData("/api/ppm/portfolios/not-a-key")]
    [InlineData("/api/ppm/projects/NO-SUCH-PROJECT-KEY/tasks/NOT-A-TASK-KEY")]
    [InlineData("/api/ppm/projects/ZZZZ/tasks/NOT-A-TASK-KEY")]
    [InlineData("/api/organization/teams/ENG-TEAM/backlog")]
    [InlineData("/api/work/workspaces/ATLAS/work-items/NOT-A-WORK-ITEM")]
    public async Task Get_AnswersNotFound_ForAMalformedKey(string route)
    {
        // Arrange
        var user = await _factory.CreateAuthenticatedClient(
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Projects),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios),
            ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.WorkItems));

        // Act
        var response = await user.Client.GetAsync(route, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
