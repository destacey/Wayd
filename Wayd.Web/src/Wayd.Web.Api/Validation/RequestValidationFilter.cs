using Microsoft.AspNetCore.Mvc.Filters;

namespace Wayd.Web.Api.Validation;

/// <summary>
/// Validates each action argument with the FluentValidation validator registered for its runtime type before the
/// action runs, recording every failure in <c>ModelState</c> under the failure's property path.
/// </summary>
/// <remarks>
/// The filter only records; it never answers. <c>[ApiController]</c>'s model-state filter turns an invalid
/// <c>ModelState</c> into the response through <c>InvalidModelStateResponseFactory</c>, so a validator's failures
/// produce the same 422 problem details as a binding failure. That needs this filter to run first, which
/// <see cref="Order"/> ensures.
/// <para>
/// The runtime type, not the parameter's, picks the validator: a polymorphic body declared as its abstract base is
/// validated by the validator for the concrete request it bound to, which <c>Include</c>s the base's rules.
/// </para>
/// <para>
/// Only the argument itself is validated, never its children one by one: a validator that should cover a nested
/// object or collection says so with <c>SetValidator</c> or <c>ChildRules</c>.
/// </para>
/// </remarks>
public sealed class RequestValidationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Ahead of <c>ModelStateInvalidFilter</c>, which runs at -2000 and would otherwise answer before any
    /// validator's failures were recorded.
    /// </summary>
    public const int Order = -2100;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
                context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
        }

        await next();
    }
}
