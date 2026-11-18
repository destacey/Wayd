using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Events.Links;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A record's links over HTTP, on a team: the endpoints need only a signed-in account, and what the caller may read
/// and change is the team's own answer.
/// </summary>
/// <remarks>
/// Run against the booted host so the handlers' generated code, the owner type registrations and the save that
/// records each link event are the real ones.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class LinksEndpointTests(WaydSqlServerApiFactory factory)
{
    private static readonly string _viewTeams = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Teams);
    private static readonly string _updateTeams = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Teams);

    private readonly WaydSqlServerApiFactory _factory = factory;

    private static string Route(Guid teamId) => $"/api/links/organization.team/{teamId}";

    private static object Link(string name = "Team wiki", string url = "https://example.com/wiki") => new { name, url };

    private static async Task<JsonElement> ReadBody(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken)).RootElement;

    private async Task<Guid> CreateTeam()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();

        var code = $"L{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var team = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new CreateTeamCommand($"Links {code}", new TeamCode(code), null, new LocalDate(2026, 1, 1)),
            TestContext.Current.CancellationToken);
        Assert.True(team.IsSuccess, team.IsFailure ? team.Error : null);
        return team.Value.Id;
    }

    [Fact]
    public async Task SomeoneWhoCanEditTheTeam_AddsChangesAndRemovesItsLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateTeam();
        var editor = await _factory.CreateAuthenticatedClient(_viewTeams, _updateTeams);

        // Act
        var created = await editor.Client.PostAsJsonAsync(Route(teamId), Link(), ct);
        var linkId = (await ReadBody(created)).GetGuid();
        var listed = await ReadBody(await editor.Client.GetAsync(Route(teamId), ct));
        var updated = await editor.Client.PutAsJsonAsync($"{Route(teamId)}/{linkId}", Link("Handbook", "https://example.com/handbook"), ct);
        var fetched = await ReadBody(await editor.Client.GetAsync($"{Route(teamId)}/{linkId}", ct));
        var deleted = await editor.Client.DeleteAsync($"{Route(teamId)}/{linkId}", ct);
        var afterDelete = await ReadBody(await editor.Client.GetAsync(Route(teamId), ct));

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(listed.GetProperty("canManage").GetBoolean());
        Assert.Equal("Team wiki", Assert.Single(listed.GetProperty("links").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal("Handbook", fetched.GetProperty("name").GetString());
        Assert.Equal("organization.team", fetched.GetProperty("ownerType").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(afterDelete.GetProperty("links").EnumerateArray());

        // Every change is recorded on the link and filed under the team it is on.
        using var scope = _factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<IActivityLogDbContext>().ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateType == "Link" && a.AggregateId == linkId
                && a.RelatedAggregates.Any(r => r.AggregateType == "Team" && r.AggregateId == teamId))
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .Select(a => a.EventType)
            .ToListAsync(ct);
        Assert.Equal([nameof(LinkAddedEvent), nameof(LinkDetailsUpdatedEvent), nameof(LinkRemovedEvent)], events);
    }

    [Fact]
    public async Task SomeoneWhoCanOnlyViewTheTeam_ReadsItsLinks_AndIsRefusedAChange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateTeam();
        var editor = await _factory.CreateAuthenticatedClient(_viewTeams, _updateTeams);
        var linkId = (await ReadBody(await editor.Client.PostAsJsonAsync(Route(teamId), Link(), ct))).GetGuid();
        var viewer = await _factory.CreateAuthenticatedClient(_viewTeams);

        // Act
        var listed = await viewer.Client.GetAsync(Route(teamId), ct);
        var created = await viewer.Client.PostAsJsonAsync(Route(teamId), Link("Another", "https://example.com/another"), ct);
        var updated = await viewer.Client.PutAsJsonAsync($"{Route(teamId)}/{linkId}", Link("Renamed"), ct);
        var deleted = await viewer.Client.DeleteAsync($"{Route(teamId)}/{linkId}", ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var body = await ReadBody(listed);
        Assert.False(body.GetProperty("canManage").GetBoolean());
        Assert.Single(body.GetProperty("links").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    [Fact]
    public async Task SomeoneWhoCannotViewTheTeam_IsToldItsLinksDoNotExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateTeam();
        var stranger = await _factory.CreateAuthenticatedClient();

        // Act
        var listed = await stranger.Client.GetAsync(Route(teamId), ct);
        var created = await stranger.Client.PostAsJsonAsync(Route(teamId), Link(), ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);
    }

    [Fact]
    public async Task AnAddressThatIsNotAWebAddress_IsRefused_AndOneTheTeamAlreadyLinksTo_IsKept()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateTeam();
        var editor = await _factory.CreateAuthenticatedClient(_viewTeams, _updateTeams);
        await editor.Client.PostAsJsonAsync(Route(teamId), Link(), ct);

        // Act
        var notAnAddress = await editor.Client.PostAsJsonAsync(Route(teamId), Link("Script", "javascript:alert(1)"), ct);
        var duplicate = await editor.Client.PostAsJsonAsync(Route(teamId), Link("Same address"), ct);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notAnAddress.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
    }

    [Fact]
    public async Task ALinkOnAnotherTeam_IsNotFoundThroughThisOne_AndIsLeftAsItWas()
    {
        // Arrange — editing one team gives nothing on the links of another
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateTeam();
        var otherTeamId = await CreateTeam();
        var editor = await _factory.CreateAuthenticatedClient(_viewTeams, _updateTeams);
        var linkId = (await ReadBody(await editor.Client.PostAsJsonAsync(Route(otherTeamId), Link(), ct))).GetGuid();

        // Act
        var fetched = await editor.Client.GetAsync($"{Route(teamId)}/{linkId}", ct);
        var updated = await editor.Client.PutAsJsonAsync($"{Route(teamId)}/{linkId}", Link("Renamed"), ct);
        var deleted = await editor.Client.DeleteAsync($"{Route(teamId)}/{linkId}", ct);
        var stillThere = await ReadBody(await editor.Client.GetAsync(Route(otherTeamId), ct));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal("Team wiki", Assert.Single(stillThere.GetProperty("links").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task AKindOfRecordThatCannotCarryLinks_IsABadRequest()
    {
        // Arrange
        var editor = await _factory.CreateAuthenticatedClient(_viewTeams, _updateTeams);

        // Act
        var route = $"/api/links/organization.nothing/{Guid.NewGuid()}";
        var read = await editor.Client.GetAsync(route, TestContext.Current.CancellationToken);
        var written = await editor.Client.PostAsJsonAsync(route, Link(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, read.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, written.StatusCode);
    }
}
