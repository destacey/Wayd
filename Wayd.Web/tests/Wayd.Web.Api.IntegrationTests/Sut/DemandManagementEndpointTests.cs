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
/// Drives Demand Management through HTTP: the feature gate, who may call each endpoint (two claims, with desk
/// membership and the request's own people granting the rest), enum codes on the wire, and a private desk's
/// requests staying absent for everyone outside it.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class DemandManagementEndpointTests(WaydSqlServerApiFactory factory)
{
    private const string Requests = "/api/demand-management/intake-requests";
    private const string Desks = "/api/demand-management/intake-desks";
    private const string Channels = "/api/demand-management/intake-channels";

    private static readonly string View = Permission(ApplicationAction.View);
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
    public async Task APrivateDesksRequest_IsAbsentForOutsiders_AndReadableByItsRequesterAndAgentWithoutAnyClaim()
    {
        // Arrange — submitting, following your own request and working a desk need no claim
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var requester = await SeedEmployee();
        var agent = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var requesterClient = await _factory.CreateAuthenticatedClient(requester);
        var agentClient = await _factory.CreateAuthenticatedClient(agent);
        var outsider = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View);

        var deskId = await CreateDesk(admin.Client, "Private", agents: [agent]);
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
        var asAgent = await agentClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Key}", ct);

        // Assert
        outsiderList.GetArrayLength().Should().Be(0);
        outsiderGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outsiderHistory.StatusCode.Should().Be(HttpStatusCode.NotFound);

        asRequester.GetProperty("substance").GetProperty("description").GetString().Should().Be("Detail only the desk should read");
        asRequester.GetProperty("summary").GetProperty("needBy").GetProperty("basis").GetString().Should().Be("Contractual");
        asRequester.GetProperty("summary").GetProperty("needBy").GetProperty("family").GetString().Should().Be("FixedForUs");
        asRequester.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        asRequester.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        asAgent.GetProperty("substance").GetProperty("description").GetString().Should().Be("Detail only the desk should read");
        asAgent.GetProperty("canRemoveParticipants").GetBoolean().Should().BeTrue();
        asAgent.GetProperty("canDelete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ASummaryOnlyDesksRequest_GivesOutsidersTheSummaryButNoSubstance()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var outsider = await _factory.CreateAuthenticatedClient(await SeedEmployee(), View);
        var deskId = await CreateDesk(admin.Client, "SummaryOnly");
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
    public async Task ARequestOnAnOpenDesk_IsAbsentForASignedInUserWithoutView()
    {
        // Arrange — browsing other people's requests is what View grants
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var noClaims = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var deskId = await CreateDesk(admin.Client, "Open");
        var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Dashboard for churn",
            description = "Weekly churn by segment",
            deskId,
            channelId = await FirstChannel(requesterClient.Client),
        }, ct);
        var request = (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;

        // Act
        var list = await noClaims.Client.GetFromJsonAsync<JsonElement>($"{Requests}?deskId={deskId}", ct);
        var get = await noClaims.Client.GetAsync($"{Requests}/{request.Key}", ct);

        // Assert
        list.GetArrayLength().Should().Be(0);
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRequest_IsADeskAdminsAndRefusedToAnAgent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var deskAdmin = await SeedEmployee();
        var agent = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var deskAdminClient = await _factory.CreateAuthenticatedClient(deskAdmin);
        var agentClient = await _factory.CreateAuthenticatedClient(agent);
        var deskId = await CreateDesk(admin.Client, "Open", agents: [agent], admins: [deskAdmin]);
        var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Retire the old portal",
            description = "Nobody uses it",
            deskId,
            channelId = await FirstChannel(requesterClient.Client),
        }, ct);
        var request = (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;

        // Act
        var asAgent = await agentClient.Client.DeleteAsync($"{Requests}/{request.Id}", ct);
        var asDeskAdmin = await deskAdminClient.Client.DeleteAsync($"{Requests}/{request.Id}", ct);

        // Assert
        asAgent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        asDeskAdmin.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateDesk_ByItsDeskAdminSucceeds_ButNotByAnAgentNorForVisibility()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var deskAdmin = await SeedEmployee();
        var agent = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var deskAdminClient = await _factory.CreateAuthenticatedClient(deskAdmin);
        var agentClient = await _factory.CreateAuthenticatedClient(agent);
        var deskId = await CreateDesk(admin.Client, "Open", agents: [agent], admins: [deskAdmin]);
        object Body(string name, string visibility, int expectedRevision = 0) => new
        {
            id = deskId,
            expectedRevision,
            name,
            description = "Anything you need built",
            visibility,
            isRequesterSelectable = true,
            isOpen = true,
            requesterMayAddParticipants = true,
            members = new object[]
            {
                new { employeeId = deskAdmin, role = "Admin" },
                new { employeeId = agent, role = "Agent" },
            },
        };

        // Act
        var renamedByAgent = await agentClient.Client.PutAsJsonAsync($"{Desks}/{deskId}", Body($"Desk {Guid.NewGuid():N}"[..20], "Open"), ct);
        var madePrivateByDeskAdmin = await deskAdminClient.Client.PutAsJsonAsync($"{Desks}/{deskId}", Body($"Desk {Guid.NewGuid():N}"[..20], "Private"), ct);
        var renamedByDeskAdmin = await deskAdminClient.Client.PutAsJsonAsync($"{Desks}/{deskId}", Body($"Desk {Guid.NewGuid():N}"[..20], "Open"), ct);
        var staleRename = await deskAdminClient.Client.PutAsJsonAsync($"{Desks}/{deskId}", Body($"Desk {Guid.NewGuid():N}"[..20], "Open"), ct);
        var asDeskAdmin = await deskAdminClient.Client.GetFromJsonAsync<JsonElement>($"{Desks}/{deskId}", ct);

        // Assert
        renamedByAgent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        madePrivateByDeskAdmin.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        renamedByDeskAdmin.StatusCode.Should().Be(HttpStatusCode.NoContent);
        staleRename.StatusCode.Should().Be(HttpStatusCode.Conflict);
        asDeskAdmin.GetProperty("revision").GetInt32().Should().Be(1);
        asDeskAdmin.GetProperty("canManage").GetBoolean().Should().BeTrue();
        asDeskAdmin.GetProperty("canAdminister").GetBoolean().Should().BeFalse();
        asDeskAdmin.GetProperty("waiting").GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DeleteDesk_NeedsAdminister_AndRefusesADeskWithRequests()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var deskAdmin = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var deskAdminClient = await _factory.CreateAuthenticatedClient(deskAdmin);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var emptyDeskId = await CreateDesk(admin.Client, "Open", admins: [deskAdmin]);
        var busyDeskId = await CreateDesk(admin.Client, "Open");
        await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Keep me",
            description = "A request on the desk",
            deskId = busyDeskId,
            channelId = await FirstChannel(requesterClient.Client),
        }, ct);

        // Act
        var byDeskAdmin = await deskAdminClient.Client.DeleteAsync($"{Desks}/{emptyDeskId}", ct);
        var busy = await admin.Client.DeleteAsync($"{Desks}/{busyDeskId}", ct);
        var empty = await admin.Client.DeleteAsync($"{Desks}/{emptyDeskId}", ct);

        // Assert
        byDeskAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        busy.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        empty.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RerouteRequest_ToALessPrivateDesk_NeedsConfirmation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var agent = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var agentClient = await _factory.CreateAuthenticatedClient(agent);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var privateDeskId = await CreateDesk(admin.Client, "Private", agents: [agent]);
        var openDeskId = await CreateDesk(admin.Client, "Open");
        var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
        {
            title = "Sensitive ask",
            description = "Written for the private desk",
            deskId = privateDeskId,
            channelId = await FirstChannel(requesterClient.Client),
        }, ct);
        var request = (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;
        var asAgent = await agentClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Id}", ct);

        // Act
        var unconfirmed = await agentClient.Client.PostAsJsonAsync($"{Requests}/{request.Id}/reroute",
            new { deskId = openDeskId, confirmWiderVisibility = false }, ct);
        var confirmed = await agentClient.Client.PostAsJsonAsync($"{Requests}/{request.Id}/reroute",
            new { deskId = openDeskId, confirmWiderVisibility = true }, ct);
        var moved = await requesterClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{request.Id}", ct);
        var toNowhere = await agentClient.Client.PostAsJsonAsync($"{Requests}/{request.Id}/reroute",
            new { deskId = Guid.NewGuid(), confirmWiderVisibility = true }, ct);

        // Assert
        asAgent.GetProperty("canReroute").GetBoolean().Should().BeTrue();
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        moved.GetProperty("summary").GetProperty("desk").GetProperty("id").GetGuid().Should().Be(openDeskId);
        toNowhere.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            members = Array.Empty<object>(),
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
        var roles = await viewer.Client.GetFromJsonAsync<JsonElement>($"{Desks}/roles", ct);
        var bases = await viewer.Client.GetFromJsonAsync<JsonElement>($"{Requests}/need-by-bases", ct);
        var families = await viewer.Client.GetFromJsonAsync<JsonElement>($"{Requests}/need-by-basis-families", ct);

        // Assert
        families.EnumerateArray().Select(f => (f.GetProperty("code").GetString(), f.GetProperty("name").GetString()))
            .Should().Equal(("FixedForUs", "Fixed for us"), ("ChosenByUs", "We chose it"));
        visibilities.EnumerateArray().Select(v => v.GetProperty("code").GetString())
            .Should().Equal("Open", "SummaryOnly", "Private");
        roles.EnumerateArray().Select(v => (v.GetProperty("code").GetString(), v.GetProperty("name").GetString()))
            .Should().Equal(("Admin", "Desk Admin"), ("Agent", "Agent"));
        bases.EnumerateArray().Single(b => b.GetProperty("code").GetString() == "Commitment")
            .GetProperty("recordsCommitment").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task EmployeeOptions_AreReadableBySignedInUserWithNoClaim()
    {
        // Arrange — submitting on someone's behalf needs the options, and submitting needs no claim
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var employeeId = await SeedEmployee();
        var viewer = await _factory.CreateAuthenticatedClient(await SeedEmployee());

        // Act
        var response = await viewer.Client.GetAsync("/api/demand-management/employee-options", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        options.EnumerateArray().Should().Contain(o => o.GetProperty("id").GetGuid() == employeeId);
    }

    [Fact]
    public async Task CreateRequest_WithoutATitle_IsRejectedBeforeTheHandler()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var requester = await _factory.CreateAuthenticatedClient(await SeedEmployee());

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

    [Fact]
    public async Task ChangeStatus_IsAnAgentsWithNoClaim_AndWithdrawIsTheRequesters()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await SetFlag(true);
        var agent = await SeedEmployee();
        var admin = await _factory.CreateAuthenticatedClient(Administer);
        var agentClient = await _factory.CreateAuthenticatedClient(agent);
        var requesterClient = await _factory.CreateAuthenticatedClient(await SeedEmployee());
        var deskId = await CreateDesk(admin.Client, "Open", agents: [agent]);
        async Task<ObjectIdAndKey> Submit(string title)
        {
            var created = await requesterClient.Client.PostAsJsonAsync(Requests, new
            {
                title,
                description = "Finance needs a CSV export",
                deskId,
                channelId = await FirstChannel(requesterClient.Client),
            }, ct);
            return (await created.Content.ReadFromJsonAsync<ObjectIdAndKey>(ct))!;
        }
        var toDecline = await Submit("Export invoices");
        var toWithdraw = await Submit("Export receipts");

        // Act
        var options = await agentClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{toDecline.Id}/status-options", ct);
        var declined = options.EnumerateArray().Single(o => o.GetProperty("name").GetString() == "Declined");
        var withoutReason = await agentClient.Client.PostAsJsonAsync($"{Requests}/{toDecline.Id}/status",
            new { statusId = declined.GetProperty("id").GetGuid() }, ct);
        var byRequester = await requesterClient.Client.PostAsJsonAsync($"{Requests}/{toDecline.Id}/status",
            new { statusId = declined.GetProperty("id").GetGuid(), reason = "Changed my mind" }, ct);
        var byAgent = await agentClient.Client.PostAsJsonAsync($"{Requests}/{toDecline.Id}/status",
            new { statusId = declined.GetProperty("id").GetGuid(), reason = "Budget went elsewhere" }, ct);
        var asRequester = await requesterClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{toDecline.Id}", ct);
        var history = await requesterClient.Client.GetFromJsonAsync<JsonElement>($"{Requests}/{toDecline.Id}/status-history", ct);
        var withdrawnByAgent = await agentClient.Client.PostAsJsonAsync($"{Requests}/{toWithdraw.Id}/withdraw", new { }, ct);
        var withdrawn = await requesterClient.Client.PostAsJsonAsync($"{Requests}/{toWithdraw.Id}/withdraw", new { note = "Solved it another way" }, ct);

        // Assert
        declined.GetProperty("needsReason").GetBoolean().Should().BeTrue();
        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        byRequester.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        byAgent.StatusCode.Should().Be(HttpStatusCode.NoContent, await byAgent.Content.ReadAsStringAsync(ct));
        var status = asRequester.GetProperty("summary").GetProperty("status");
        status.GetProperty("name").GetString().Should().Be("Declined");
        status.GetProperty("category").GetString().Should().Be("Removed", "a closed set crosses the API as its code");
        asRequester.GetProperty("substance").GetProperty("statusReason").GetString().Should().Be("Budget went elsewhere");
        asRequester.GetProperty("canEdit").GetBoolean().Should().BeFalse();
        history.EnumerateArray().First().GetProperty("reason").GetString().Should().Be("Budget went elsewhere");
        withdrawnByAgent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        withdrawn.StatusCode.Should().Be(HttpStatusCode.NoContent, await withdrawn.Content.ReadAsStringAsync(ct));
    }

    private static string Permission(string action) => ApplicationPermission.NameFor(action, ApplicationResource.IntakeRequests);

    private async Task<Guid> CreateDesk(HttpClient admin, string visibility, Guid[]? agents = null, Guid[]? admins = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var members = (agents ?? []).Select(id => new { employeeId = id, role = "Agent" })
            .Concat((admins ?? []).Select(id => new { employeeId = id, role = "Admin" }));
        var response = await admin.PostAsJsonAsync(Desks, new
        {
            name = $"Desk {Guid.NewGuid():N}"[..20],
            description = "Anything you need built",
            visibility,
            members,
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
