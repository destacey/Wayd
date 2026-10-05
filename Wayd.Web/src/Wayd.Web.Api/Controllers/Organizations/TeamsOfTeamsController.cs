using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using TeamsMemberCommands = Wayd.Organization.Application.Teams.Commands;
using TeamsMemberQueries = Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.Application.TeamsOfTeams.Dtos;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations;
using Wayd.Web.Api.Models.Organizations.TeamOfTeams;
using Wayd.Web.Api.Models.Organizations.Teams;
using Wayd.Web.Api.Models.Organizations.TeamsOfTeams;
using Wayd.Web.Api.Models.Planning.Risks;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/teams-of-teams")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Teams)]
public class TeamsOfTeamsController : ControllerBase
{
    private readonly ILogger<TeamsOfTeamsController> _logger;
    private readonly IDispatcher _dispatcher;

    public TeamsOfTeamsController(ILogger<TeamsOfTeamsController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a list of teams of teams in the organization.", "")]
    [McpTool("TeamsOfTeams_GetList", "List teams of teams")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamOfTeamsListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var teams = await _dispatcher.Send(new GetTeamOfTeamsListQuery(includeInactive), cancellationToken);
        return Ok(teams.OrderBy(e => e.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get team of teams details using the key.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOfTeamsDetailsDto>> GetById(int id)
    {
        var team = await _dispatcher.Send(new GetTeamOfTeamsQuery(id));

        return team is not null
            ? Ok(team)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get activity history for the team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetTeamOfTeamsActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrCode}/allocation")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation(
        "Report where the completed work of a team of teams, and every team beneath it, went.",
        "Groups the Requirement-tier work items completed between `from` and `to` (yyyy-MM-dd, inclusive, UTC, max 366 days) by portfolio, program, project, strategic theme or work type. Each team's work rolls up to the parent it had on the day the work was done. Measures: Count, StoryPoints (only teams sizing in story points), or TeamEffort (each team's split in its own sizing method, StoryPoints, Effort, Size or Count, as on the day the work was done, combined by share of completed items; recommended for teams of teams). Work with no project is its own group.")]
    [McpTool("TeamsOfTeams_GetAllocation", "Report team of teams allocation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamAllocationDto>> GetAllocation(
        string idOrCode,
        [FromQuery] GetTeamAllocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.TryToQuery(idOrCode, out var query, out var error))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(error!, HttpContext));

        var result = await _dispatcher.Send(query!, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Teams)]
    [OpenApiOperation("Create a team of teams.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(CreateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Key }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, UpdateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Deactivate a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Deactivate(Guid id, [FromBody] DeactivateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToDeactivateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    //[HttpDelete("{id}")]
    //[MustHavePermission(ApplicationAction.Delete, ApplicationResource.TeamsOfTeams)]
    //[OpenApiOperation("Delete an team.", "")]
    //public async Task<string> Delete(string id)
    //{
    //    throw new NotImplementedException();
    //}

    #region Team Memberships

    [HttpGet("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get parent team memberships.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _dispatcher.Send(new GetTeamMembershipsQuery(id), cancellationToken);

        return Ok(memberships);
    }

    [HttpPost("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Add a parent team membership.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddTeamMembership(Guid id, [FromBody] AddTeamMembershipRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToTeamOfTeamsAddParentTeamMembershipCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/team-memberships/{teamMembershipId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team membership.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateTeamMembership(Guid id, Guid teamMembershipId, [FromBody] UpdateTeamMembershipRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));
        else if (teamMembershipId != request.TeamMembershipId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(teamMembershipId), nameof(request.TeamMembershipId), HttpContext));

        var result = await _dispatcher.Send(request.ToTeamOfTeamsUpdateTeamMembershipCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/team-memberships/{teamMembershipId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Remove a parent team membership.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveTeamMembership(Guid id, Guid teamMembershipId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveTeamMembershipCommand(id, teamMembershipId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Memberships

    #region Operating Models

    [HttpGet("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the operating model history for a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TeamOfTeamsOperatingModelDetailsDto>>> GetOperatingModels(Guid id, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var models = await _dispatcher.Send(new GetTeamOfTeamsOperatingModelsQuery(id), cancellationToken);

        return Ok(models);
    }

    [HttpGet("{id}/operating-models/defaults")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the values a new operating model for a team of teams is pre-filled with.", "The time zone is the one its parent team of teams had in effect on the start date (yyyy-MM-dd), else the system default.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatingModelDefaultsDto>> GetOperatingModelDefaults(Guid id, [FromQuery] string startDate, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(startDate, out var parsedStartDate) || parsedStartDate is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var defaults = await _dispatcher.Send(new TeamsMemberQueries.GetOperatingModelDefaultsQuery(id, parsedStartDate.Value), cancellationToken);

        return defaults is not null
            ? Ok(defaults)
            : NotFound();
    }

    [HttpGet("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a specific operating model for a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOfTeamsOperatingModelDetailsDto>> GetOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var operatingModel = await _dispatcher.Send(new GetTeamOfTeamsOperatingModelQuery(id, operatingModelId), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpPost("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Set a new operating model for a team of teams.", "")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetOperatingModel(Guid id, [FromBody] SetTeamOfTeamsOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToSetTeamOfTeamsOperatingModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetOperatingModel), new { id, operatingModelId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Correct an existing operating model for a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOperatingModel(Guid id, Guid operatingModelId, [FromBody] UpdateTeamOfTeamsOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateTeamOfTeamsOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Delete the current operating model from a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteTeamOfTeamsOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Operating Models

    #region Risks

    [HttpGet("{id}/risks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get team risks.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RiskListDto>>> GetRisks(Guid id, CancellationToken cancellationToken, bool includeClosed = false)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risks = await _dispatcher.Send(new GetRisksQuery(id, includeClosed), cancellationToken);

        return Ok(risks);
    }

    [HttpGet("{id}/risks/{riskIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a team of teams risk by Id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRiskById(Guid id, string riskIdOrKey, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risk = await _dispatcher.Send(new GetRiskQuery(riskIdOrKey), cancellationToken);

        return risk is not null && risk.Team?.Id == id
            ? Ok(risk)
            : NotFound();
    }

    [HttpPost("{id}/risks")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Create a risk for a team of teams.", "")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskIdOrKey = result.Value.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team of teams risk.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateRisk(Guid id, Guid riskId, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));
        else if (riskId != request.RiskId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(riskId), nameof(request.RiskId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Risks

    #region Team Members

    [HttpGet("{id}/members")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the members of a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new TeamsMemberQueries.GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Add a member to a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddMember(Guid id, [FromBody] AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.AddTeamMemberCommand(id, request.EmployeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team of teams member's roles.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateMember(Guid id, Guid employeeId, [FromBody] UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.UpdateTeamMemberCommand(id, employeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Remove a member from a team of teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.RemoveTeamMemberCommand(id, employeeId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Members
}
