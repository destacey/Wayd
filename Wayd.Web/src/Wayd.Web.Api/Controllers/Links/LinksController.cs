using Wayd.Links.Commands;
using Wayd.Links.Models;
using Wayd.Links.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Links;

namespace Wayd.Web.Api.Controllers.Links;

[Route("api/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class LinksController : ControllerBase
{
    private readonly ILogger<LinksController> _logger;
    private readonly IDispatcher _dispatcher;

    public LinksController(ILogger<LinksController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet("{objectId}/list")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Links)]
    [OpenApiOperation("Get a list of links for a specific objectId.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LinkDto>>> GetList(Guid objectId, CancellationToken cancellationToken)
    {
        var links = await _dispatcher.Send(new GetLinksQuery(objectId), cancellationToken);
        return Ok(links);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Links)]
    [OpenApiOperation("Get a link by id.", "")]
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
    [OpenApiOperation("Create a link.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> Create([FromBody] CreateLinkRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateLinkCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Links)]
    [OpenApiOperation("Update a link.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LinkDto>> Update(Guid id, [FromBody] UpdateLinkRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateLinkCommand(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Links)]
    [OpenApiOperation("Delete a link.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteLinkCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
