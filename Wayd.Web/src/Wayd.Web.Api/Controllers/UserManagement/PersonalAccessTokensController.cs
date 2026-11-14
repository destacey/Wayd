using Wayd.Common.Application.Identity.PersonalAccessTokens.Commands;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Dtos;
using Wayd.Common.Application.Identity.PersonalAccessTokens.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.PersonalAccessTokens;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/personal-access-tokens")]
[ApiController]
public class PersonalAccessTokensController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PersonalAccessTokensController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [EndpointSummary("Get all personal access tokens for the current user.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<PersonalAccessTokenDto>>> GetMyTokens(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetMyPersonalAccessTokensQuery(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}")]
    [EndpointSummary("Get a personal access token by ID.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonalAccessTokenDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetPersonalAccessTokenQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost]
    [EndpointSummary("Create a new personal access token.")]
    [EndpointDescription("Returns the plaintext token - this is the ONLY time it will be visible! Answers 403 unless the caller is signed in to the web app: a token cannot mint another.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreatePersonalAccessTokenResult>> Create(CreatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        if (!WebAppSession.IsCurrent(HttpContext))
            return WebAppSessionRequired();

        var command = request.ToCreatePersonalAccessTokenCommand();
        var result = await _dispatcher.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [EndpointSummary("Update a personal access token's name and/or expiration date.")]
    [EndpointDescription("Answers 403 unless the caller is signed in to the web app: a token cannot extend itself or another.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, UpdatePersonalAccessTokenRequest request, CancellationToken cancellationToken)
    {
        if (!WebAppSession.IsCurrent(HttpContext))
            return WebAppSessionRequired();

        var command = request.ToUpdatePersonalAccessTokenCommand(id);
        var result = await _dispatcher.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/revoke")]
    [EndpointSummary("Revoke a personal access token.")]
    [EndpointDescription("Revoked tokens are kept for audit purposes.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RevokePersonalAccessTokenCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Permanently delete a personal access token.")]
    [EndpointDescription("This removes the token from the database entirely.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeletePersonalAccessTokenCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    /// <summary>
    /// The 403 for a caller authenticated other than by a sign-in to the web app. A token that could mint or extend
    /// tokens would let a leaked one outlive its revocation, and a connected application create credentials of its own.
    /// </summary>
    private ObjectResult WebAppSessionRequired() =>
        Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Personal access tokens can be created or changed only while signed in to Wayd.");
}
