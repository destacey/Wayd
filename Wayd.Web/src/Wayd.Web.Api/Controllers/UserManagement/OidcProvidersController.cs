using Wayd.Common.Application.Identity.OidcProviders.Commands;
using Wayd.Common.Application.Identity.OidcProviders.Dtos;
using Wayd.Common.Application.Identity.OidcProviders.Queries;
using Wayd.Common.Application.Identity.Users;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.OidcProviders;

namespace Wayd.Web.Api.Controllers.UserManagement;

/// <summary>
/// Admin CRUD for OIDC providers. Distinct from <c>AuthController</c>'s
/// <c>GET /api/auth/providers</c>, which is anonymous and returns a narrower
/// shape for the unauthenticated login page. This controller exposes the full
/// configuration (incl. AllowedTenantIds) and requires the OidcProviders
/// permission set.
/// </summary>
[Route("api/user-management/oidc-providers")]
[ApiVersionNeutral]
[ApiController]
public class OidcProvidersController(ISender sender, IUserService userService) : ControllerBase
{
    private readonly ISender _sender = sender;
    private readonly IUserService _userService = userService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.OidcProviders)]
    [OpenApiOperation("List all configured OIDC providers.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OidcProviderListItemDto>>> GetList(CancellationToken cancellationToken)
    {
        var providers = await _sender.Send(new GetOidcProvidersQuery(), cancellationToken);
        return Ok(providers);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.OidcProviders)]
    [OpenApiOperation("Get OIDC provider detail by id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OidcProviderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var provider = await _sender.Send(new GetOidcProviderQuery(id), cancellationToken);
        return provider is not null ? Ok(provider) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.OidcProviders)]
    [OpenApiOperation("Create a new OIDC provider.", "")]
    [ProducesResponseType(typeof(OidcProviderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create([FromBody] CreateOidcProviderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOidcProviderCommand(
            request.Name,
            request.DisplayName,
            request.ProviderType,
            request.Authority,
            request.ClientId,
            request.Audience,
            request.Scopes,
            request.AllowedTenantIds,
            request.ClockSkewSeconds,
            request.IsEnabled,
            request.AllowAutoRegistration,
            request.RequireEmployeeRecord,
            request.DefaultRoleId);

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.OidcProviders)]
    [OpenApiOperation("Update an OIDC provider.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateOidcProviderRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var command = new UpdateOidcProviderCommand(
            request.Id,
            request.DisplayName,
            request.Authority,
            request.ClientId,
            request.Audience,
            request.Scopes,
            request.AllowedTenantIds,
            request.ClockSkewSeconds,
            request.IsEnabled,
            request.AllowAutoRegistration,
            request.RequireEmployeeRecord,
            request.DefaultRoleId);

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.OidcProviders)]
    [OpenApiOperation("Delete an OIDC provider.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(DeleteOidcProviderResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteOidcProviderCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ToBadRequestObject(HttpContext));
        }

        // 409 distinguishes "you can't delete this yet because users still
        // depend on it" from "you sent bad data". The body carries the
        // active-identity count so the UI can show the admin how many users
        // need to be rebound first.
        return result.Value.Deleted
            ? NoContent()
            : Conflict(result.Value);
    }

    [HttpPost("{id:guid}/test-discovery")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.OidcProviders)]
    [OpenApiOperation("Fetch the provider's OIDC discovery document and report success/failure.", "")]
    [ProducesResponseType(typeof(TestOidcProviderDiscoveryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TestOidcProviderDiscoveryResult>> TestDiscovery(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new TestOidcProviderDiscoveryCommand(id), cancellationToken);

        // Note: a discovery failure (timeout, 404, bad JSON) returns 200 OK
        // with Success=false. The 400 path is reserved for the request being
        // structurally bad (e.g. unknown id) — see DeleteOidcProviderResult
        // for the same distinction.
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id:guid}/migration-candidates")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Users)]
    [OpenApiOperation("List Entra users on the given source tenant with no pending migration — the candidates for a bulk tenant migration.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TenantMigrationCandidateDto>>> GetMigrationCandidates(
        Guid id, [FromQuery] string sourceTenantId, CancellationToken cancellationToken)
    {
        var result = await _userService.GetTenantMigrationCandidates(id, sourceTenantId, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id:guid}/pending-migrations")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Users)]
    [OpenApiOperation("List users on the given provider with a staged-but-incomplete tenant migration.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PendingTenantMigrationDto>>> GetPendingMigrations(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetPendingTenantMigrations(id, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id:guid}/stage-tenant-migration")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Stage a tenant migration for multiple Entra users. Each rebind completes on that user's next sign-in from the target tenant.", "")]
    [ProducesResponseType(typeof(BulkTenantMigrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BulkTenantMigrationResult>> StageBulkTenantMigration(
        Guid id, [FromBody] StageBulkTenantMigrationRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.StageBulkTenantMigration(
            new StageBulkTenantMigrationCommand(id, request.SourceTenantId, request.TargetTenantId, request.UserIds),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
