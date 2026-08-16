using System.Linq.Expressions;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Application.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Models.Admin;
using Wayd.Web.Api.Models.Admin.BackgroundJobs;

namespace Wayd.Web.Api.Controllers.Admin;

[Route("api/admin/background-jobs")]
[ApiVersionNeutral]
[ApiController]
public class BackgroundJobsController(ILogger<BackgroundJobsController> logger, IJobService jobService, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<BackgroundJobsController> _logger = logger;
    private readonly IJobService _jobService = jobService;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("job-types")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get a list of all job types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BackgroundJobTypeDto>>> GetJobTypes(CancellationToken cancellationToken)
    {
        // TODO how do we determine what is active rather than returning all types
        var types = await _dispatcher.Send(new GetBackgroundJobTypesQuery(), cancellationToken);
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
                _jobService.EnqueueSystem(() => jobManager.RunPortfolioRankRebalance(cancellationToken));
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
        var jobType = (BackgroundJobType)request.JobTypeId;

        // Not every job type can run on a schedule (the data-replication syncs are triggered by the
        // flows that change the data). The UI filters its picker on the same set, so reaching here
        // means a direct API call — answer with a 400 rather than letting the switch below throw.
        if (!SchedulableBackgroundJobTypes.Contains(jobType))
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest($"Job type {jobType} cannot be scheduled.", HttpContext));
        }

        _jobService.AddOrUpdate(request.JobId, GetMethodCall(jobType), () => request.CronExpression);

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
                // Unreachable: SchedulableBackgroundJobTypes gates entry, and this switch must cover
                // every member of it. A miss here means the two have drifted.
                _ => throw new ArgumentOutOfRangeException(nameof(jobType), jobType, "Job type is marked schedulable but has no recurring invocation mapped.")
            };
        }
    }

    [HttpGet("jobs")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get a page of jobs in the given lifecycle state.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<JobsResponse> GetJobs(
        [FromQuery] JobStateFilter state = JobStateFilter.Processing,
        [FromQuery] int pageNumber = 0,
        [FromQuery] int pageSize = 50)
    {
        var page = _jobService.GetJobs(state, Math.Max(pageNumber, 0), Math.Clamp(pageSize, 1, 500));
        return Ok(JobsResponse.From(page));
    }

    [HttpGet("jobs/{jobId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get the full detail of a job, including its arguments, state history, and failure stack trace.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<JobDetailResponse> GetJobDetail(string jobId)
    {
        var detail = _jobService.GetJobDetail(jobId);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(JobDetailResponse.From(detail));
    }

    [HttpGet("statistics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get job counts by lifecycle state.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<JobStatisticsResponse> GetStatistics() =>
        Ok(JobStatisticsResponse.From(_jobService.GetStatistics()));

    [HttpGet("servers")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get the job servers currently polling for work.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<JobServerResponse>> GetServers() =>
        Ok(_jobService.GetServers().Select(JobServerResponse.From));

    [HttpGet("recurring")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Get all registered recurring jobs.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RecurringJobResponse>> GetRecurringJobs() =>
        Ok(_jobService.GetRecurringJobs().Select(RecurringJobResponse.From));

    [HttpDelete("recurring/{recurringJobId}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Remove a recurring job registration.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult RemoveRecurringJob(string recurringJobId) =>
        _jobService.RemoveRecurringJob(recurringJobId) ? NoContent() : NotFound();

    [HttpPost("jobs/{jobId}/requeue")]
    [MustHavePermission(ApplicationAction.Run, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Requeue a job for another attempt.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult RequeueJob(string jobId) =>
        _jobService.Requeue(jobId) ? Accepted() : NotFound();

    [HttpDelete("jobs/{jobId}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.BackgroundJobs)]
    [OpenApiOperation("Delete a job, moving it to the deleted state.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult DeleteJob(string jobId) =>
        _jobService.Delete(jobId) ? NoContent() : NotFound();
}