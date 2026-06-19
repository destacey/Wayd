using Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Dtos;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.PersonalAccessTokens;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/personal-access-tokens")]
[ApiVersionNeutral]
[ApiController]
public class PersonalAccessTokensController : ControllerBase
{
    private readonly ISender _sender;

    public PersonalAccessTokensController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [OpenApiOperation("Get all personal access tokens for the current user.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<PersonalAccessTokenDto>>> GetMyTokens(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMyPersonalAccessTokensQuery(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}")]
    [OpenApiOperation("Get a personal access token by ID.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonalAccessTokenDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPersonalAccessTokenQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost]
    [OpenApiOperation("Create a new personal access token.", "Returns the plaintext token - this is the ONLY time it will be visible!")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreatePersonalAccessTokenResult>> Create(CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var command = request.ToCreatePersonalAccessTokenCommand();
        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [OpenApiOperation("Update a personal access token's name and/or expiration date.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, UpdatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var command = request.ToUpdatePersonalAccessTokenCommand(id);
        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/revoke")]
    [OpenApiOperation("Revoke a personal access token.", "Revoked tokens are kept for audit purposes.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RevokePersonalAccessTokenCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [OpenApiOperation("Permanently delete a personal access token.", "This removes the token from the database entirely.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeletePersonalAccessTokenCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
