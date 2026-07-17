using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Search;
using Wayd.Common.Application.Search.Dtos;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers;

[Route("api/search")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
public class SearchController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [OpenApiOperation("Global search across all modules.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GlobalSearchResultDto>> Search(
        [FromQuery] string query, CancellationToken cancellationToken, [FromQuery] int maxResultsPerCategory = 5)
    {
        var result = await _dispatcher.Send(
            new GlobalSearchQuery(query, maxResultsPerCategory), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
