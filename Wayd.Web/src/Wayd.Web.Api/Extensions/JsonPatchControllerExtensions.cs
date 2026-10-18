using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Serves a JSON Patch (RFC 6902) endpoint from a record's existing update request and command, so a full update
/// and a partial one share one command and one set of validation rules.
/// </summary>
public static class JsonPatchControllerExtensions
{
    /// <summary>
    /// Applies a JSON Patch to the current record expressed as its update request, validates the patched request,
    /// and sends the same command the full update sends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The patch never reaches the domain as a partial change: <paramref name="toRequest"/> turns the current record
    /// into a complete update request, the patch edits that request, and <paramref name="send"/> dispatches it whole.
    /// A field the patch does not name therefore keeps its current value — provided <paramref name="toRequest"/>
    /// copies it. A request property that conversion leaves unset is cleared by every patch.
    /// </para>
    /// <para>
    /// The patched request is validated by the <see cref="IValidator{T}"/> registered for its runtime type, so a
    /// polymorphic request (one patch document over several derived request types) is validated by the derived
    /// type's rules. A request type with no registered validator throws rather than going unvalidated.
    /// </para>
    /// <para>
    /// Answers 400 when there is no patch document, 404 with <paramref name="notFoundMessage"/> when
    /// <paramref name="load"/> finds nothing, 422 with a validation problem holding
    /// every patch and validation error, 400 with the problem details of a command the domain refuses, and 204 on
    /// success.
    /// </para>
    /// </remarks>
    /// <typeparam name="TCurrent">The record as the endpoint's get query returns it.</typeparam>
    /// <typeparam name="TRequest">The record's update request, as declared by the patch document.</typeparam>
    /// <param name="controller">The controller serving the endpoint.</param>
    /// <param name="patchDocument">The patch the client sent, whose paths name <typeparamref name="TRequest"/> properties.</param>
    /// <param name="load">Reads the current record, or <see langword="null"/> when it does not exist.</param>
    /// <param name="notFoundMessage">The body of the 404 answered when <paramref name="load"/> finds nothing.</param>
    /// <param name="toRequest">Converts the current record into a complete update request.</param>
    /// <param name="send">Dispatches the update command built from the patched request.</param>
    /// <param name="cancellationToken">Cancels the load, the validation and the command.</param>
    public static async Task<ActionResult> Patch<TCurrent, TRequest>(
        this ControllerBase controller,
        JsonPatchDocument<TRequest>? patchDocument,
        Func<CancellationToken, Task<TCurrent?>> load,
        string notFoundMessage,
        Func<TCurrent, TRequest> toRequest,
        Func<TRequest, CancellationToken, Task<Result>> send,
        CancellationToken cancellationToken)
        where TCurrent : class
        where TRequest : class
    {
        if (patchDocument == null)
            return controller.BadRequest("Patch document cannot be null.");

        var current = await load(cancellationToken);
        if (current is null)
            return controller.NotFound(notFoundMessage);

        var request = toRequest(current);

        patchDocument.ApplyTo(request, error =>
        {
            controller.ModelState.AddModelError(error.AffectedObject.GetType().Name, error.ErrorMessage);
        });

        var validationResult = await ValidatorFor(controller.HttpContext.RequestServices, request.GetType())
            .ValidateAsync(new ValidationContext<object>(request), cancellationToken);
        foreach (var error in validationResult.Errors)
        {
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!controller.ModelState.IsValid)
            return controller.ValidationProblem(
                modelStateDictionary: controller.ModelState,
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var result = await send(request, cancellationToken);

        return result.IsSuccess
            ? controller.NoContent()
            : controller.BadRequest(result.ToBadRequestObject(controller.HttpContext));
    }

    private static IValidator ValidatorFor(IServiceProvider services, Type requestType)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);

        return services.GetService(validatorType) as IValidator
            ?? throw new InvalidOperationException(
                $"No {nameof(IValidator)} is registered for the patch request type '{requestType.FullName}'. A patched request must be validated before its command is sent.");
    }
}
