using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Models;
using Wayd.Infrastructure.Auth;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Drives Demand Management through HTTP: the feature gate, the permissions on each endpoint, enum codes
/// on the wire, and a private desk's requests staying absent for everyone outside it.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DemandManagementEndpointTests(WaydSqlServerApiFactory factory)
{
    private const string Requests = "/api/demand-management/intake-requests";
    private const string Desks = "/api/demand-management/intake-desks";
    private const string Channels = "/api/demand-management/intake-channels";

    private static readonly string View = Permission(ApplicationAction.View);
    private static readonly string Create = Permission(ApplicationAction.Create);
    private static readonly string Review = Permission(ApplicationAction.Review);
    private static readonly string Administer = Permission(ApplicationAction.Administer);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Endpoints_AnswerNotFound_WhileTheFeatureFlagIsOff()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var client = await _factory.CreateAuthenticatedClient(View);
        await SetFlag(false);

        // Act
        var response = await client.Client.GetAsync(Requests, ct);

        // Assert
        await SetFlag(true);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task APrivateDesksRequest_IsAbsentForOutsiders_AndReadableByItsRequesterAndReviewer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var requester = await SeedEmployee();
        var reviewer = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer, View);
        var requesterClient = await _factory.CreateAuthenticatedClient(requester, View, Create);
        var reviewerClient = await _factory.CreateAuthenticatedClient(reviewer, View, Review);
        var outsider = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View);

        var deskId = await CreateDesk(admin.Client, "Private", [reviewer]);
        var channelId = await FirstChannel(requesterClient.Client);
        var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Confidential reorganisation tooling",
            description = "Detail only the desk should read",
            deskId,
            channelId,
            needBy = new { date = "2026-12-31", basis = "Contractual", reference = "MSA clause 12" },
        }, ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;

        // Act
        var outsiderList = await outsider.Client.GetFromJsonAsync<JsonElement>($"{Requests}?deskId={deskId}", ct);
        var outsiderGet = await outsider.Client.GetAsync($"{Requests}/{request.Key}", ct);
        var outsiderHistory = await outsider.Client.GetAsync($"{Requests}/{request.Id}/activities", ct);
        var asRequester = await requesterClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Key}", ct);
        var asReviewer = await reviewerClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Key}", ct);

        // Assert
        outsiderList.GetArrayLength().Should().Be(0);
        outsiderGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outsiderHistory.StatusCode.Should().Be(HttpStatusCode.NotFound);

        asRequester.GetProperty("substance").GetProperty("description").GetString().Should().Be("Detail only the desk should read");
        asRequester.GetProperty("summary").GetProperty("needBy").GetProperty("basis").GetString().Should().Be("Contractual");
        asRequester.GetProperty("summary").GetProperty("needBy").GetProperty("family").GetString().Should().Be("FixedForUs");
        asRequester.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        asReviewer.GetProperty("canReview").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ASummaryOnlyDesksRequest_GivesOutsidersTheSummaryButNoSubstance()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var admin = await _factory.CreateAuthenticatedClient(Administer, View);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View, Create);
        var outsider = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View);
        var deskId = await CreateDesk(admin.Client, "SummaryOnly", []);
        var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Laptop refresh",
            description = "Replace the 2021 laptops",
            deskId,
            channelId = await FirstChannel(requesterClient.Client),
        }, ct);
        var request = (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;

        // Act
        var result = await outsider.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Id}", ct);

        // Assert
        result.GetProperty("summary").GetProperty("title").GetString().Should().Be("Laptop refresh");
        result.GetProperty("substance").ValueKind.Should().Be(JsonValueKind.Null);
        result.GetProperty("canEdit").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateDesk_NeedsAdminister()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var viewer = await _factory.CreateAuthenticatedClient(View);

        // Act
        var response = await viewer.Client.PostAsJsonAsync(Desks, new
        {
            name = $"Desk {Guid.NewGuid():N}"[..20],
            description = "Anything",
            visibility = "Open",
            reviewerIds = Array.Empty<Guid>(),
        }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Lookups_PublishEnumCodes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var viewer = await _factory.CreateAuthenticatedClient(View);

        // Act
        var visibilities = await viewer.Client.GetFromJsonAsync<JsonElement>($"{Desks}/visibilities", ct);
        var bases = await viewer.Client.GetFromJsonAsync<JsonElement>($"{Requests}/need-by-bases", ct);

        // Assert
        visibilities.EnumerateArray().Select(v => v.GetProperty("code").GetString())
            .Should().Equal("Open", "SummaryOnly", "Private");
        bases.EnumerateArray().Single(b => b.GetProperty("code").GetString() == "Commitment")
            .GetProperty("recordsCommitment").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateRequest_WithoutATitle_IsRejectedBeforeTheHandler()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var requester = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View, Create);

        // Act
        var response = await requester.Client.PostAsJsonAsync(Requests, new
        {
            title = "",
            description = "Something",
            deskId = Guid.NewGuid(),
            channelId = Guid.NewGuid(),
        }, ct);

        // Assert
        ((int)response.StatusCode).Should().BeOneOf(400, 422);
    }

    private static string Permission(string action) => ApplicationPermission.NameFor(action, ApplicationResource.IntakeRequests);

    private async Task<Guid> CreateDesk(HttpClient admin, string visibility, Guid[] reviewerIds)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await admin.PostAsJsonAsync(Desks, new
        {
            name = $"Desk {Guid.NewGuid():N}"[..20],
            description = "Anything you need built",
            visibility,
            reviewerIds,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!.Id;
    }

    private static async Task<Guid> FirstChannel(HttpClient client)
    {
        var channels = await client.GetFromJsonAsync<JsonElement>(Channels, TestContext.Current.CancellationToken);
        return channels.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private async Task<Guid> SeedEmployee()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var dbContext = scope.ServiceProvider.GetRequiredService<IWaydDbContext>();
        var now = NodaTime.SystemClock.Instance.GetCurrentInstant();
        var employee = Employee.Create(
            new PersonName("Intake", null, "Endpoint"),
            Guid.NewGuid().ToString("N")[..12],
            now,
            new EmailAddress(Guid.NewGuid().ToString("N")[..12] + "@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            now);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return employee.Id;
    }

    private async Task SetFlag(bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId("integration-test-harness");
        var flagId = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.DemandManagement)
            .Select(f => f.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var toggled = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
            new ToggleFeatureFlagCommand(flagId, enabled), TestContext.Current.CancellationToken);
        Assert.True(toggled.IsSuccess, toggled.IsFailure ? toggled.Error : null);
    }
}
