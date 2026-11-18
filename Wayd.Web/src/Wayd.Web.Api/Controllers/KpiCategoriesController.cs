using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The organization's KPI categories, which every scorecard groups by.
/// </summary>
/// <remarks>
/// Anyone signed in reads them, since every KPI form offers them; changing them needs
/// <c>Permissions.Kpis.Administer</c>, checked by the handlers so a read-only token is refused too.
/// </remarks>
[Route("api/kpi-categories")]
[ApiController]
[Authorize]
[FeatureGate(FeatureFlags.Names.Kpis)]
public class KpiCategoriesController(IDispatcher dispatcher) : ControllerBase
{
    private const string ManageDocs = "Needs `Permissions.Kpis.Administer`; returns 403 otherwise.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("Get the KPI categories.")]
    [EndpointDescription("Returns the categories in order, each with how many KPIs have it. Inactive ones are left out unless `includeInactive` is true.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<KpiCategoryDto>>> GetCategories([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok(await _dispatcher.Send(new GetKpiCategoriesQuery(includeInactive), cancellationToken));

    [HttpPost]
    [EndpointSummary("Define a KPI category.")]
    [EndpointDescription($"Adds an active category, last in the list, and returns its id. Names are unique, ignoring case. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> Create([FromBody] KpiCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CreateKpiCategoryCommand(request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetCategories), null, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Change a KPI category's name and description.")]
    [EndpointDescription($"Names are unique, ignoring case. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] KpiCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateKpiCategoryCommand(id, request.Name, request.Description), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPost("{id:guid}/activate")]
    [EndpointSummary("Offer a KPI category for new KPIs again.")]
    [EndpointDescription(ManageDocs)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetKpiCategoryActiveCommand(id, IsActive: true), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPost("{id:guid}/deactivate")]
    [EndpointSummary("Stop offering a KPI category for new KPIs.")]
    [EndpointDescription($"KPIs that have it keep it. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetKpiCategoryActiveCommand(id, IsActive: false), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a KPI category no KPI uses.")]
    [EndpointDescription($"Returns 400 when any KPI, open or closed, has it: deactivate it instead. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteKpiCategoryCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPut("order")]
    [EndpointSummary("Put the KPI categories in a new order.")]
    [EndpointDescription($"`categoryIds` lists every category, active and inactive, each once. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reorder([FromBody] ReorderKpiCategoriesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ReorderKpiCategoriesCommand(request.CategoryIds), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private ActionResult ErrorResult(string error)
    {
        if (KpiAccessErrors.IsNotFound(error))
            return NotFound();

        if (KpiAccessErrors.IsForbidden(error))
            return Problem(detail: error, statusCode: StatusCodes.Status403Forbidden);

        if (KpiAccessErrors.IsConflict(error))
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);

        return BadRequest(ProblemDetailsExtensions.ForBadRequest(error, HttpContext));
    }
}
