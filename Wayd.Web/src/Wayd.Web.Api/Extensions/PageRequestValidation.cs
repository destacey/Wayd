using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wayd.Common.Application.Models;

namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Checks a <see cref="PageRequest"/> against the shared paging bounds for an action that reads its page without a
/// query, so its callers get the same default, bounds and 422 as every other paged list.
/// </summary>
/// <remarks>
/// For actions over host plumbing (Hangfire's job store, Wolverine's dead letters) that stay out of the handler
/// pipeline, where <see cref="PagedQueryValidator{TQuery}"/> would otherwise run. The rules are that validator's.
/// </remarks>
public static class PageRequestValidation
{
    /// <summary>Carries the page to the shared validator, which validates queries rather than bare pages.</summary>
    private sealed record Request(PageRequest Paging) : IPagedQuery;

    /// <summary>The shared paging rules, unchanged.</summary>
    private sealed class RequestValidator : PagedQueryValidator<Request>;

    private static readonly RequestValidator Validator = new();

    /// <summary>
    /// Adds each paging failure to <paramref name="modelState"/>, named after the query parameter, and says whether
    /// there were none.
    /// </summary>
    public static bool IsValid(this PageRequest paging, ModelStateDictionary modelState)
    {
        var result = Validator.Validate(new Request(paging));
        foreach (var error in result.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return result.IsValid;
    }
}
