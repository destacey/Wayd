using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Authorization;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// User administration is delegable, but never reaches a privileged account, grants a privileged role, or changes
/// the caller's own roles or employee link. Checked against the booted host, since the controllers call the user
/// service without dispatching and only the host shows which rules actually run.
/// </summary>
[Collection(SignInApiTestCollection.Name)]
public sealed class UserAdministrationGuardTests(WaydSqlServerApiFactory factory)
{
    private const string PrivilegedRefusal = "Manage Privileged Users";

    private static readonly string ViewUsers = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Users);
    private static readonly string CreateUsers = ApplicationPermission.NameFor(ApplicationAction.Create, ApplicationResource.Users);
    private static readonly string UpdateUsers = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Users);
    private static readonly string UpdateUserRoles = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.UserRoles);
    private static readonly string ViewRoles = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Roles);
    private static readonly string ManageRoles = ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.Roles);
    private static readonly string UpdateProjects = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Projects);

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task ResetPassword_IsForbidden_ForAPrivilegedTarget_AndAllowedForOneHoldingOrdinaryPermissionsTheCallerLacks()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, UpdateUsers);
        var privileged = await _factory.CreateAuthenticatedClient(ManageRoles);
        var ordinary = await _factory.CreateAuthenticatedClient(UpdateProjects);
        var body = new { newPassword = "Reset-Passw0rd-1" };

        // Act
        var onPrivileged = await caller.Client.PutAsJsonAsync($"/api/user-management/users/{privileged.UserId}/reset-password", body, TestContext.Current.CancellationToken);
        var onOrdinary = await caller.Client.PutAsJsonAsync($"/api/user-management/users/{ordinary.UserId}/reset-password", body, TestContext.Current.CancellationToken);

        // Assert
        await ShouldBeForbidden(onPrivileged, PrivilegedRefusal);
        onOrdinary.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivate_IsForbidden_ForAPrivilegedTarget_AndAllowedForAnOrdinaryOne()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, UpdateUsers);
        var privileged = await _factory.CreateAuthenticatedClient(ManageRoles);
        var ordinary = await _factory.CreateAuthenticatedClient(ViewUsers);

        // Act
        var onPrivileged = await caller.Client.PutAsync($"/api/user-management/users/{privileged.UserId}/deactivate", null, TestContext.Current.CancellationToken);
        var onOrdinary = await caller.Client.PutAsync($"/api/user-management/users/{ordinary.UserId}/deactivate", null, TestContext.Current.CancellationToken);

        // Assert
        await ShouldBeForbidden(onPrivileged, PrivilegedRefusal);
        onOrdinary.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateUser_AssignsOrdinaryRoles_ButNotOnesGrantingAPrivilegedPermission()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, CreateUsers);
        var ordinary = await CreateRole(ViewUsers, UpdateProjects);
        var privileged = await CreateRole(ViewUsers, ManageRoles);

        // Act
        var allowed = await caller.Client.PostAsJsonAsync("/api/user-management/users", NewUser(ordinary), TestContext.Current.CancellationToken);
        var refused = await caller.Client.PostAsJsonAsync("/api/user-management/users", NewUser(privileged), TestContext.Current.CancellationToken);
        var admin = await caller.Client.PostAsJsonAsync("/api/user-management/users", NewUser(ApplicationRoles.Admin), TestContext.Current.CancellationToken);

        // Assert
        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
        await ShouldBeForbidden(refused, PrivilegedRefusal);
        await ShouldBeForbidden(admin, PrivilegedRefusal);
    }

    [Fact]
    public async Task CreateUser_RunsTheCommandsValidation()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, CreateUsers);
        var request = NewUser("No Such Role");

        // Act
        var response = await caller.Client.PostAsJsonAsync("/api/user-management/users", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("No Such Role");
    }

    [Fact]
    public async Task UpdateUser_IsForbidden_WhenTheCallerChangesTheEmployeeLinkedToTheirOwnAccount()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, UpdateUsers);
        var email = await EmailOf(caller.UserId);

        // Act
        var linking = await caller.Client.PutAsJsonAsync($"/api/user-management/users/{caller.UserId}",
            new { id = caller.UserId, firstName = "Integration", lastName = "Test", email, employeeId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
        var renaming = await caller.Client.PutAsJsonAsync($"/api/user-management/users/{caller.UserId}",
            new { id = caller.UserId, firstName = "Renamed", lastName = "Test", email }, TestContext.Current.CancellationToken);

        // Assert
        await ShouldBeForbidden(linking, "your own account");
        renaming.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ManageUserRoles_IsForbidden_OnTheCallersOwnAccount_AndForAPrivilegedRole()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, UpdateUserRoles);
        var target = await _factory.CreateAuthenticatedClient(ViewUsers);
        var ordinary = await CreateRole(UpdateProjects);
        var privileged = await CreateRole(ManageRoles);

        // Act
        var own = await caller.Client.PostAsJsonAsync($"/api/user-management/users/{caller.UserId}/roles",
            new { userId = caller.UserId, roleNames = new[] { ordinary } }, TestContext.Current.CancellationToken);
        var granted = await caller.Client.PostAsJsonAsync($"/api/user-management/users/{target.UserId}/roles",
            new { userId = target.UserId, roleNames = new[] { ordinary } }, TestContext.Current.CancellationToken);
        var refused = await caller.Client.PostAsJsonAsync($"/api/user-management/users/{target.UserId}/roles",
            new { userId = target.UserId, roleNames = new[] { ordinary, privileged } }, TestContext.Current.CancellationToken);

        // Assert
        await ShouldBeForbidden(own, "your own roles");
        granted.StatusCode.Should().Be(HttpStatusCode.OK);
        await ShouldBeForbidden(refused, PrivilegedRefusal);
    }

    [Fact]
    public async Task TheOwnAccountRules_HoldWhenTheCallerSendsTheirIdInAnotherCase()
    {
        // Arrange — the database matches ids without regard to case, so the rules must too.
        var caller = await _factory.CreateAuthenticatedClient(ViewUsers, UpdateUsers, UpdateUserRoles);
        var ownId = caller.UserId.ToUpperInvariant();
        var role = await CreateRoleId(permissions: [UpdateProjects]);
        var email = await EmailOf(caller.UserId);

        // Act
        var joining = await caller.Client.PostAsJsonAsync("/api/user-management/users/manage-role",
            new { roleId = role, userIdsToAdd = new[] { ownId }, userIdsToRemove = Array.Empty<string>() }, TestContext.Current.CancellationToken);
        var linking = await caller.Client.PutAsJsonAsync($"/api/user-management/users/{ownId}",
            new { id = ownId, firstName = "Integration", lastName = "Test", email, employeeId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        // Assert
        await ShouldBeForbidden(joining, "your own roles");
        await ShouldBeForbidden(linking, "your own account");
    }

    [Fact]
    public async Task ManagingRoles_NeedsRolesManage()
    {
        // Arrange
        var manager = await _factory.CreateAuthenticatedClient(ManageRoles);
        var viewer = await _factory.CreateAuthenticatedClient(ViewRoles);

        // Act
        var created = await manager.Client.PostAsJsonAsync("/api/user-management/roles",
            new { name = $"Created {Guid.NewGuid():N}" }, TestContext.Current.CancellationToken);
        var refused = await viewer.Client.PostAsJsonAsync("/api/user-management/roles",
            new { name = $"Refused {Guid.NewGuid():N}" }, TestContext.Current.CancellationToken);

        // Assert
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatingARolesPermissions_RefusesAPermissionTheCatalogDoesNotDefine()
    {
        // Arrange
        var caller = await _factory.CreateAuthenticatedClient(ManageRoles);
        var roleId = await CreateRoleId();

        // Act
        var response = await caller.Client.PutAsJsonAsync($"/api/user-management/roles/{roleId}/permissions",
            new { roleId, permissions = new[] { ViewUsers, "Permissions.RoleClaims.Update" } }, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Permissions.RoleClaims.Update");
    }

    private static async Task ShouldBeForbidden(HttpResponseMessage response, string because)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain(because);
    }

    private sealed record NewUserBody(string firstName, string lastName, string email, string loginProvider, string password, string[] roleNames);

    private static NewUserBody NewUser(string roleName) => new(
        "Created", "User", $"created-{Guid.NewGuid():N}@integration.test", LoginProviders.Wayd, "Created-Passw0rd-1", [roleName]);

    private async Task<string?> EmailOf(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();
        return await scope.ServiceProvider.GetRequiredService<IUserService>().GetEmailAsync(userId);
    }

    /// <summary>Creates a role granting <paramref name="permissions"/> and answers its name.</summary>
    private async Task<string> CreateRole(params string[] permissions)
    {
        var name = $"Guard Test Role {Guid.NewGuid():N}";
        await CreateRoleId(name, permissions);
        return name;
    }

    private async Task<string> CreateRoleId(string? name = null, params string[] permissions)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var id = await roles.CreateOrUpdate(new CreateOrUpdateRoleCommand(null, name ?? $"Guard Test Role {Guid.NewGuid():N}", null));
        (await roles.UpdatePermissions(new UpdateRolePermissionsCommand(id, [.. permissions]), TestContext.Current.CancellationToken))
            .IsSuccess.Should().BeTrue();
        return id;
    }
}
