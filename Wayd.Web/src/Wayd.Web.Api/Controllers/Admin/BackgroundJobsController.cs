using System.Linq.Expressions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Application.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Models.Admin;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/admin/background-jobs")]
[ApiVersionNeutral]
[ApiController]
public class BackgroundJobsController(ILogger<BackgroundJobsController> logger, IJobService jobService, ISender sender) : ControllerBase
{
    private readonly ILogger<BackgroundJobsController> _logger = logger;
    private readonly IJobService _jobService = jobService;
    private readonly ISender _sender = sender;

    [HttpGet("job-types")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get a list of all job types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BackgroundJobTypeDto>>> GetJobTypes(CancellationToken cancellationToken)
    {
        // TODO how do we determine what is active rather than returning all types
        var types = await _sender.Send(new GetBackgroundJobTypesQuery(), cancellationToken);
        return Ok(types.OrderBy(c => c.Order));
    }

    [HttpGet("running")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get a list of running jobs.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<BackgroundJobDto>> GetRunningJobs()
    {
        var jobs = _jobService.GetRunningJobs();
        return Ok(jobs);
    }

    [HttpPost("run")]
    [MustHavePermission(ApplicationAction.Run, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Run a background job.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Run(int jobTypeId, [FromServices] IJobManager jobManager, CancellationToken cancellationToken)
    {
        var jobType = (BackgroundJobType)jobTypeId;

        // TODO: should this code be moved to the manager?
        switch (jobType)
        {
            case BackgroundJobType.PeopleFullSync:
                _jobService.Enqueue(() => jobManager.RunPeopleSync(SyncType.Full, SyncTriggerSource.Manual, null, cancellationToken));
                break;
            case BackgroundJobType.PeopleDiffSync:
                // Connectors that don't support incremental fall back to Full inside the runner
                // (PeopleSyncRunner.SourceSupportsIncremental gates the watermark lookup), so this
                // is safe to expose even when the only active connection is Entra (Full-only).
                _jobService.Enqueue(() => jobManager.RunPeopleSync(SyncType.Differential, SyncTriggerSource.Manual, null, cancellationToken));
                break;
            case BackgroundJobType.WorkFullSync:
                _jobService.Enqueue(() => jobManager.RunWorkSync(SyncType.Full, SyncTriggerSource.Manual, null, cancellationToken));
                break;
            case BackgroundJobType.WorkDiffSync:
                _jobService.Enqueue(() => jobManager.RunWorkSync(SyncType.Differential, SyncTriggerSource.Manual, null, cancellationToken));
                break;
            case BackgroundJobType.TeamGraphSync:
                _jobService.Enqueue(() => jobManager.RunSyncTeamsWithGraphTables(cancellationToken));
                break;
            case BackgroundJobType.IterationsSync:
                _jobService.Enqueue(() => jobManager.RunSyncIterations(cancellationToken));
                break;
            case BackgroundJobType.StrategicThemesSync:
                _jobService.Enqueue(() => jobManager.RunSyncStrategicThemes(cancellationToken));
                break;
            case BackgroundJobType.ProjectsSync:
                _jobService.Enqueue(() => jobManager.RunSyncProjects(cancellationToken));
                break;
            case BackgroundJobType.TeamsSync:
                _jobService.Enqueue(() => jobManager.RunSyncTeams(cancellationToken));
                break;
            case BackgroundJobType.PortfolioRankRebalance:
                _jobService.Enqueue(() => jobManager.RunPortfolioRankRebalance(cancellationToken));
                break;
            default:
                _logger.LogWarning("Unknown job type {jobType} requested", jobType);
                return BadRequest(ProblemDetailsExtensions.ForBadRequest($"Unknown job type {jobType} requested.", HttpContext));
        }
        return Accepted();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Run, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Create a recurring background job.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Create([FromBody] CreateRecurringJobRequest request, [FromServices] IJobManager jobManager, CancellationToken cancellationToken)
    {
        _jobService.AddOrUpdate(request.JobId, GetMethodCall((BackgroundJobType)request.JobTypeId), () => request.CronExpression);

        return Accepted();

        Expression<Func<Task>> GetMethodCall(BackgroundJobType jobType)
        {
            return jobType switch
            {
                BackgroundJobType.PeopleFullSync => () => jobManager.RunPeopleSync(SyncType.Full, SyncTriggerSource.Scheduled, null, cancellationToken),
                // Connectors that don't support incremental fall back to Full inside the runner —
                // safe to schedule even when the only active connection is Entra (Full-only).
                BackgroundJobType.PeopleDiffSync => () => jobManager.RunPeopleSync(SyncType.Differential, SyncTriggerSource.Scheduled, null, cancellationToken),
                BackgroundJobType.WorkFullSync => () => jobManager.RunWorkSync(SyncType.Full, SyncTriggerSource.Scheduled, null, cancellationToken),
                BackgroundJobType.WorkDiffSync => () => jobManager.RunWorkSync(SyncType.Differential, SyncTriggerSource.Scheduled, null, cancellationToken),
                BackgroundJobType.TeamGraphSync => () => jobManager.RunSyncTeamsWithGraphTables(cancellationToken),
                BackgroundJobType.PortfolioRankRebalance => () => jobManager.RunPortfolioRankRebalance(cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(jobType), jobType, "Unknown job type requested")
            };
        }
    }
}
