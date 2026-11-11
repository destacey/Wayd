using Wayd.Links.Commands;
using Wayd.Work.Application.Persistence;
using Wayd.Links.Models;
using Wayd.Links.Queries;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Links;

namespace Wayd.Web.Api.Controllers.Links;

[Route("api/[controller]")]
[ApiController]
public class LinksController : ControllerBase
{
    private readonly ILogger<LinksController> _logger;
    private readonly IDispatcher _dispatcher;
    private readonly IWorkDbContext _workDbContext;

    public LinksController(ILogger<LinksController> logger, IDispatcher dispatcher, IWorkDbContext workDbContext)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _workDbContext = workDbContext;
    }

    [HttpGet("{objectId}/list")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Links)]
    [EndpointSummary("Get a list of links for a specific objectId.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LinkDto>>> GetList(Guid objectId, CancellationToken cancellationToken)
    {
        var links = await _dispatcher.Send(new GetLinksQuery(objectId), cancellationToken);
        return Ok(links);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Links)]
    [EndpointSummary("Get a link by id.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LinkDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var link = await _dispatcher.Send(new GetLinkQuery(id), cancellationToken);
        return link is not null
            ? Ok(link)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Links)]
    [EndpointSummary("Create a link.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> Create([FromBody] CreateLinkRequest request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);
        var result = await _dispatcher.Send(request.ToCreateLinkCommand(), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ToBadRequestObject(HttpContext));
        }

        var recorded = await IncidentTimelineLinkRecording.RecordGenericLinkChangeAsync(
            _dispatcher,
            request.ObjectId,
            IncidentTimelineLinkAction.Added,
            request.Name,
            fromLabel: null,
            toLabel: null,
            cancellationToken,
            result.Value);

        if (recorded.IsFailure)
        {
            return BadRequest(recorded.ToBadRequestObject(HttpContext));
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Links)]
    [EndpointSummary("Update a link.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LinkDto>> Update(Guid id, [FromBody] UpdateLinkRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var existing = await _dispatcher.Send(new GetLinkQuery(id), cancellationToken);
        if (existing is null)
            return NotFound();

        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);
        var result = await _dispatcher.Send(request.ToUpdateLinkCommand(), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ToBadRequestObject(HttpContext));
        }

        if (!string.Equals(existing.Name, result.Value.Name, StringComparison.Ordinal)
            || !string.Equals(existing.Url, result.Value.Url, StringComparison.Ordinal))
        {
            var recorded = await IncidentTimelineLinkRecording.RecordGenericLinkChangeAsync(
                _dispatcher,
                result.Value.ObjectId,
                IncidentTimelineLinkAction.Updated,
                $"{existing.Name} → {result.Value.Name}",
                existing.Name,
                result.Value.Name,
                cancellationToken,
                result.Value.Id);

            if (recorded.IsFailure)
            {
                return BadRequest(recorded.ToBadRequestObject(HttpContext));
            }
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Links)]
    [EndpointSummary("Delete a link.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var link = await _dispatcher.Send(new GetLinkQuery(id), cancellationToken);
        if (link is null)
            return NotFound();

        await using var unitOfWork = await _workDbContext.BeginUnitOfWork(cancellationToken);
        var result = await _dispatcher.Send(new DeleteLinkCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ToBadRequestObject(HttpContext));
        }

        var recorded = await IncidentTimelineLinkRecording.RecordGenericLinkChangeAsync(
            _dispatcher,
            link.ObjectId,
            IncidentTimelineLinkAction.Removed,
            link.Name,
            fromLabel: null,
            toLabel: null,
            cancellationToken,
            id);

        if (recorded.IsFailure)
        {
            return BadRequest(recorded.ToBadRequestObject(HttpContext));
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return NoContent();
    }
}
