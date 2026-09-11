using System.Diagnostics;
using Wayd.Common.Application.Imports.Dtos;
using Wayd.Common.Application.Imports.Queries;
using Wayd.Web.Api.Controllers.Imports;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Services;

/// <summary>How long an import submission waits on its run before answering without the outcome.</summary>
public sealed record ImportResponseTiming(TimeSpan Budget, TimeSpan PollInterval)
{
    public static ImportResponseTiming FromConfiguration(IConfiguration configuration) => new(
        TimeSpan.FromSeconds(Math.Max(0, configuration.GetValue("Imports:ResponseWaitSeconds", 5))),
        TimeSpan.FromMilliseconds(250));
}

/// <summary>
/// Answers an import submission with the run itself: finished if it finished within the wait, otherwise
/// still queued or running, for the caller to follow.
/// </summary>
/// <remarks>
/// The wait is measured in time rather than rows because a row's cost differs by an order of magnitude
/// between imports, so a row count that suits one is wrong for the rest.
/// <para>
/// It has to happen here, after the submission handler has returned. Wolverine holds back a message
/// published inside a handler until that handler completes, so waiting inside it would spend the whole
/// budget on a run that has not been sent to the queue yet.
/// </para>
/// </remarks>
public sealed class ImportSubmissionResponder(IDispatcher dispatcher, ImportResponseTiming timing)
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ImportResponseTiming _timing = timing;

    public async Task<ActionResult> Respond(ControllerBase controller, Guid importProcessId, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            var run = await _dispatcher.Send(new GetImportProcessQuery(importProcessId), cancellationToken);

            if (run.IsFailure)
                return controller.BadRequest(run.ToBadRequestObject(controller.HttpContext));

            if (run.Value.IsTerminal)
                return controller.Ok(run.Value);

            if (elapsed.Elapsed + _timing.PollInterval > _timing.Budget)
                return controller.Accepted($"/{ImportsController.Route}/{importProcessId}", run.Value);

            await Task.Delay(_timing.PollInterval, cancellationToken);
        }
    }
}
