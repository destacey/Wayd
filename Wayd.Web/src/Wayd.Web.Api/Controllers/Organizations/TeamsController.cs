using CsvHelper;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.Iterations.Dtos;
using Wayd.Planning.Application.Iterations.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations;
using Wayd.Web.Api.Models.Organizations.Teams;
using Wayd.Web.Api.Models.Planning.Risks;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/teams")]
[ApiVersionNeutral]
[ApiController]
public class TeamsController(ILogger<TeamsController> logger, ISender sender, ICsvService csvService) : ControllerBase
{
    private readonly ILogger<TeamsController> _logger = logger;
    private readonly ISender _sender = sender;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a list of teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TeamListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var teams = await _sender.Send(new GetTeamsQuery(includeInactive), cancellationToken);
        return Ok(teams.OrderBy(e => e.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get team details using the key.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDetailsDto>> GetById(int id)
    {
        var team = await _sender.Send(new GetTeamQuery(id));

        return team is not null
            ? Ok(team)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Teams)]
    [OpenApiOperation("Create a team.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request.ToCreateTeamCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Key }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Teams)]
    [OpenApiOperation("Import teams and teams of teams from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedTeams = _csvService.ReadCsv<ImportTeamRequest>(file.OpenReadStream());

            List<ImportTeamDto> teams = [];
            var validator = new ImportTeamRequestValidator();
            foreach (var team in importedTeams)
            {
                var validationResults = await validator.ValidateAsync(team, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Team Code: {team.Code})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                teams.Add(team.ToImportTeamDto());
            }

            if (teams.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No teams imported.", HttpContext));

            var result = await _sender.Send(new ImportTeamsCommand(teams), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpPost("members/import")]
    [MustHavePermission(ApplicationAction.ManageTeamMemberships, ApplicationResource.Teams)]
    [OpenApiOperation("Import team members (staffing) from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ImportMembers([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedMembers = _csvService.ReadCsv<ImportTeamMemberRequest>(file.OpenReadStream());

            List<ImportTeamMemberDto> members = [];
            var validator = new ImportTeamMemberRequestValidator();
            foreach (var member in importedMembers)
            {
                var validationResults = await validator.ValidateAsync(member, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Team Code: {member.TeamCode}, Employee Number: {member.EmployeeNumber})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                members.Add(member.ToImportTeamMemberDto());
            }

            if (members.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No team members imported.", HttpContext));

            var result = await _sender.Send(new ImportTeamMembersCommand(members), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpPost("team-memberships/import")]
    [MustHavePermission(ApplicationAction.ManageTeamMemberships, ApplicationResource.Teams)]
    [OpenApiOperation("Import the team hierarchy (parent/child team memberships) from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ImportTeamMemberships([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedMemberships = _csvService.ReadCsv<ImportTeamMembershipRequest>(file.OpenReadStream());

            List<ImportTeamMembershipDto> memberships = [];
            var validator = new ImportTeamMembershipRequestValidator();
            foreach (var membership in importedMemberships)
            {
                var validationResults = await validator.ValidateAsync(membership, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Child: {membership.ChildCode}, Parent: {membership.ParentCode})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                memberships.Add(membership.ToImportTeamMembershipDto());
            }

            if (memberships.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No team memberships imported.", HttpContext));

            var result = await _sender.Send(new ImportTeamMembershipsCommand(memberships), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _sender.Send(request.ToUpdateTeamCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Deactivate a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Deactivate(Guid id, [FromBody] DeactivateTeamRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _sender.Send(request.ToDeactivateTeamCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    //[HttpDelete("{id}")]
    //[MustHavePermission(ApplicationAction.Delete, ApplicationResource.Teams)]
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
    public async Task<ActionResult<IEnumerable<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetTeamMembershipsQuery(id), cancellationToken));
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
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var result = await _sender.Send(request.ToTeamAddParentTeamMembershipCommand(), cancellationToken);

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

        var result = await _sender.Send(request.ToTeamUpdateTeamMembershipCommand(), cancellationToken);

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
        var result = await _sender.Send(new RemoveTeamMembershipCommand(id, teamMembershipId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Memberships

    #region Work Items

    // TODO: update the claims check for viewing teams and work items
    [HttpGet("{idOrCode}/backlog")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation("Get the backlog for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemBacklogItemDto>>> GetTeamBacklog(string idOrCode, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTeamBacklogQuery(idOrCode), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrCode}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation("Get the work items for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetTeamWorkItems(
        string idOrCode,
        [FromQuery] WorkStatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        Instant? doneFromInstant = null;
        Instant? doneToInstant = null;

        if (doneFrom.HasValue)
        {
            var df = doneFrom.Value;
            df = df.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(df, DateTimeKind.Utc) : df.ToUniversalTime();
            doneFromInstant = Instant.FromDateTimeUtc(df);
        }

        if (doneTo.HasValue)
        {
            var dt = doneTo.Value;
            dt = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
            doneToInstant = Instant.FromDateTimeUtc(dt);
        }

        var workItems = await _sender.Send(new GetTeamWorkItemsQuery(idOrCode, statusCategories, doneFromInstant, doneToInstant), cancellationToken);

        return workItems is null
            ? NotFound()
            : Ok(workItems);
    }

    [HttpGet("{id}/dependencies")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [OpenApiOperation("Get the active dependencies for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DependencyDto>>> GetTeamDependencies(Guid id, CancellationToken cancellationToken)
    {
        var dependencies = await _sender.Send(new GetTeamDependenciesQuery(id, [DependencyState.ToDo, DependencyState.InProgress]), cancellationToken);

        return dependencies is null
            ? NotFound()
            : Ok(dependencies);
    }

    #endregion Work Items

    #region Risks

    [HttpGet("{id}/risks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get team risks.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetRisks(Guid id, CancellationToken cancellationToken, bool includeClosed = false)
    {
        var teamExists = await _sender.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risks = await _sender.Send(new GetRisksQuery(id, includeClosed), cancellationToken);

        return Ok(risks);
    }

    [HttpGet("{id}/risks/{riskIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a team risk by Id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRiskById(Guid id, string riskIdOrKey, CancellationToken cancellationToken)
    {
        var teamExists = await _sender.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risk = await _sender.Send(new GetRiskQuery(riskIdOrKey), cancellationToken);

        return risk is not null && risk.Team?.Id == id
            ? Ok(risk)
            : NotFound();
    }

    [HttpPost("{id}/risks")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Risks)]
    [OpenApiOperation("Create a risk for a team.", "")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _sender.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _sender.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Risks)]
    [OpenApiOperation("Update a team risk.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateRisk(Guid id, Guid riskId, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));
        else if (riskId != request.RiskId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(riskId), nameof(request.RiskId), HttpContext));

        var result = await _sender.Send(request.ToUpdateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Risks

    #region Operating Models

    [HttpGet("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get a specific operating model for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOperatingModelDetailsDto>> GetOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var teamExists = await _sender.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var operatingModel = await _sender.Send(new GetTeamOperatingModelQuery(id, operatingModelId), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpGet("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the operating model history for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamOperatingModelDetailsDto>>> GetOperatingModels(Guid id, CancellationToken cancellationToken)
    {
        var history = await _sender.Send(new GetTeamOperatingModelsQuery(id), cancellationToken);

        return history is not null
            ? Ok(history)
            : NotFound();
    }

    [HttpGet("{id}/operating-models/as-of")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the current operating model for a team, or the model effective on a specific date.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOperatingModelDetailsDto>> GetOperatingModelAsOf(Guid id, [FromQuery] DateTime? asOfDate, CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? localDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;

        var operatingModel = await _sender.Send(new GetTeamOperatingModelAsOfQuery(id, localDate), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpGet("operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get operating models for multiple teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TeamOperatingModelDetailsDto>>> GetOperatingModelsForTeams(
        [FromQuery] Guid[] teamIds,
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? localDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;

        var operatingModels = await _sender.Send(new GetTeamOperatingModelsForTeamsQuery(teamIds, localDate), cancellationToken);

        return Ok(operatingModels);
    }

    [HttpGet("{id}/has-ever-been-scrum")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Check if a team has ever used the Scrum methodology.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> HasEverBeenScrum(Guid id, CancellationToken cancellationToken)
    {
        var hasBeenScrum = await _sender.Send(new TeamHasEverBeenScrumQuery(id), cancellationToken);

        return hasBeenScrum is not null
            ? Ok(hasBeenScrum)
            : NotFound();
    }

    [HttpPost("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Set a new operating model for a team.", "")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetOperatingModel(Guid id, [FromBody] SetTeamOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var teamExists = await _sender.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _sender.Send(request.ToSetTeamOperatingModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetOperatingModel), new { id, operatingModelId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update an existing operating model for a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOperatingModel(Guid id, Guid operatingModelId, [FromBody] UpdateTeamOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request.ToUpdateTeamOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Delete an operating model from a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTeamOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Operating Models

    [HttpGet("{id}/sprints")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get the sprints for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SprintListDto>>> GetSprints(Guid id, CancellationToken cancellationToken)
    {
        var sprints = await _sender.Send(new GetSprintsQuery(id), cancellationToken);

        return Ok(sprints);
    }

    [HttpGet("{id}/sprints/active")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get the team's active sprint", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SprintDetailsDto>> GetActiveSprint(Guid id, CancellationToken cancellationToken)
    {
        var sprint = await _sender.Send(new GetTeamActiveSprintQuery(id), cancellationToken);

        return Ok(sprint);
    }

    [HttpGet("functional-organization-chart")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the functional organizaation chart for a given date.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FunctionalOrganizationChartDto>> GetFunctionalOrganizationChart([FromQuery] DateTime? asOfDate, CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? dateOnlyAsOfDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;
        var orgChart = await _sender.Send(new GetFunctionalOrganizationChartQuery(dateOnlyAsOfDate), cancellationToken);

        return Ok(orgChart);
    }

    #region Team Members

    [HttpGet("{id}/members")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [OpenApiOperation("Get the members of a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Add a member to a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddMember(Guid id, [FromBody] AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AddTeamMemberCommand(id, request.EmployeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Update a team member's roles.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateMember(Guid id, Guid employeeId, [FromBody] UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateTeamMemberCommand(id, employeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [OpenApiOperation("Remove a member from a team.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveTeamMemberCommand(id, employeeId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Members
}
