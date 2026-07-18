using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Commands;

public sealed class CreateExternalWorkProcessCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateExternalWorkProcessCommandHandler> logger) : ICommandHandler<CreateExternalWorkProcessCommand, IntegrationState<Guid>>
{
    private const string AppRequestName = nameof(CreateExternalWorkProcessCommand);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateExternalWorkProcessCommandHandler> _logger = logger;

    public async Task<Result<IntegrationState<Guid>>> Handle(CreateExternalWorkProcessCommand request, CancellationToken cancellationToken)
    {
        if (await _workDbContext.WorkProcesses.AnyAsync(wp => wp.ExternalId == request.ExternalWorkProcess.Id, cancellationToken))
        {
            _logger.LogWarning("{AppRequestName}: work process {WorkProcessName} already exists.", AppRequestName, request.ExternalWorkProcess.Name);
            return Result.Failure<IntegrationState<Guid>>($"Work process {request.ExternalWorkProcess.Name} already exists.");
        }

        var timestamp = _dateTimeProvider.Now;

        var workProcess = WorkProcess.CreateExternal(request.ExternalWorkProcess.Name, request.ExternalWorkProcess.Description, request.ExternalWorkProcess.Id, timestamp);

        var workTypes = await _workDbContext.WorkTypes.Where(wt => request.WorkProcessSchemes.Select(wps => wps.WorkTypeName).Contains(wt.Name)).ToArrayAsync(cancellationToken);

        foreach (var workProcessScheme in request.WorkProcessSchemes)
        {
            var workType = workTypes.FirstOrDefault(wt => wt.Name == workProcessScheme.WorkTypeName);
            if (workType is null)
            {
                _logger.LogError("{AppRequestName}: work type {WorkTypeName} not found.", AppRequestName, workProcessScheme.WorkTypeName);
                return Result.Failure<IntegrationState<Guid>>($"Work type {workProcessScheme.WorkTypeName} not found.");
            }

            workProcess.AddWorkType(workType.Id, workProcessScheme.WorkflowId, workProcessScheme.WorkTypeIsActive, timestamp);
        }

        _workDbContext.WorkProcesses.Add(workProcess);
        await _workDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{AppRequestName}: created work process {WorkProcessName}.", AppRequestName, workProcess.Name);

        return Result.Success(IntegrationState<Guid>.Create(workProcess.Id, workProcess.IsActive));
    }
}
