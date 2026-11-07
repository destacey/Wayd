using CSharpFunctionalExtensions;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers.Ppm;

/// <summary>
/// Answers a refused change to a project aggregate: 409 when another change to the project saved first, so the
/// caller knows to reload and retry, and 400 otherwise.
/// </summary>
internal static class ProjectFailureResults
{
    /// <summary>The answer for a refused change to a project, from its error.</summary>
    public static ActionResult ProjectFailure(this ControllerBase controller, string error) =>
        error == ProjectSaves.StaleError
            ? controller.Conflict(ProblemDetailsExtensions.ForConflict(error, controller.HttpContext))
            : controller.BadRequest(ProblemDetailsExtensions.ForBadRequest(error, controller.HttpContext));

    /// <summary>204 for a change to a project that succeeded; otherwise <see cref="ProjectFailure"/>.</summary>
    public static ActionResult NoContentOrProjectFailure(this ControllerBase controller, Result result) =>
        result.IsSuccess ? controller.NoContent() : controller.ProjectFailure(result.Error);
}
