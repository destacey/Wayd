using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Me;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The caller's own records, resolved from who is signed in. Every action needs only a signed-in account:
/// reading your own team memberships and allocations does not take the View Employees permission that
/// reading anyone else's does.
/// </summary>
[Route("api/me")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[McpTools("wayd-users")]
public class MeController(IDispatcher dispatcher, IUserService userService, ICurrentUser currentUser, ICurrentPrincipal currentPrincipal) : ControllerBase
{
    private const string NoLinkedEmployee = "Your account is not linked to an employee, so nothing in Wayd is recorded against you as a person. An administrator links an account to an employee on its user record in Settings.";

    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IUserService _userService = userService;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    [HttpGet]
    [EndpointSummary("Get who you are: your user account and the employee it is linked to.")]
    [EndpointDescription(
        "Call this first whenever a request is about the person asking (\"my\", \"me\", \"I\"): never list users or employees to find yourself, which needs permissions most people do not hold. Wayd records people as employees, so `employee.id` is the UUID that employeeId, role and assignee parameters take for you, and `userId` is only for parameters that say they take a user id. `employee` is null when your account is not linked to an employee. For your teams, allocations and work items use Me_GetTeamMemberships, Me_GetAllocations and Me_GetWorkItems; for projects where you hold a role, Projects_GetProjects with `role` and no `employeeId`.")]
    [McpTool("Me_GetProfile", "Get who I am")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> GetProfile(CancellationToken cancellationToken)
    {
        if (_currentUser.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (await _userService.GetAsync(userId, cancellationToken) is not { } user)
            return Unauthorized();

        return Ok(new MeResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Employee = user.Employee,
            Roles = [.. user.Roles.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase)],
        });
    }

    [HttpGet("team-memberships")]
    [EndpointSummary("List the teams and teams of teams you are a member of.")]
    [EndpointDescription(
        "Returns one entry per team or team of teams your employee belongs to, sorted by team name, each with the member roles you hold on it. Needs no View Employees permission. Membership is not allocation: Me_GetAllocations says how much of your time each team has. Returns 404 when your account is not linked to an employee.")]
    [McpTool("Me_GetTeamMemberships", "List my teams")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetTeamMemberships(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        var memberships = await _dispatcher.Send(new GetEmployeeTeamMembershipsQuery(employeeId), cancellationToken);

        return memberships is not null
            ? Ok(memberships)
            : NotLinked();
    }

    [HttpGet("allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [EndpointSummary("Get how your time is allocated to teams over time.")]
    [EndpointDescription(
        "Returns every allocation of your employee across all teams in date order: the team, the role the time is spent in when one was chosen, the date range (`end` is null when it has no end) and the `percent` of your time. Needs no View Employees permission. Requires the team-capacity-management feature flag; returns 404 when it is off or your account is not linked to an employee.")]
    [McpTool("Me_GetAllocations", "Get my allocations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeAllocationDto>>> GetAllocations(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        var allocations = await _dispatcher.Send(new GetEmployeeAllocationsQuery(employeeId), cancellationToken);

        return allocations is not null
            ? Ok(allocations)
            : NotLinked();
    }

    [HttpGet("work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the work items assigned to you.")]
    [EndpointDescription(
        "Your tickets: the backlog-level (Requirement tier) work items assigned to your employee across every workspace; epics, features and tasks are left out. `statusCategories` narrows them to any of the codes `Proposed`, `Active`, `Done` and `Removed` — pass `Proposed` and `Active` for open work. `doneFrom` and `doneTo` keep only work items finished in that window, so either one leaves out work not yet done; a time without a zone is read as UTC. Needs the Work Items view permission. Returns 404 when your account is not linked to an employee.")]
    [McpTool("Me_GetWorkItems", "List my work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetWorkItems(
        [FromQuery] StatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        return Ok(await _dispatcher.Send(
            new GetEmployeeWorkItemsQuery(employeeId, statusCategories, UtcQueryTime.ToInstant(doneFrom), UtcQueryTime.ToInstant(doneTo)),
            cancellationToken));
    }

    private NotFoundObjectResult NotLinked() =>
        NotFound(ProblemDetailsExtensions.ForNotFound(NoLinkedEmployee, HttpContext));
}
