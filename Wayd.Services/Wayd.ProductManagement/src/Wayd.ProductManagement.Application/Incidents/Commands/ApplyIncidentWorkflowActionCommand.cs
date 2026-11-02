using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

public sealed record ApplyIncidentWorkflowActionCommand(
    Guid IncidentId,
    IncidentWorkflowAction Action,
    string? Reason) : ICommand, IRequireLinkedEmployee;

public sealed class ApplyIncidentWorkflowActionCommandValidator : AbstractValidator<ApplyIncidentWorkflowActionCommand>
{
    public ApplyIncidentWorkflowActionCommandValidator()
    {
        RuleFor(c => c.IncidentId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(1024);
    }
}

public sealed class ApplyIncidentWorkflowActionCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<ApplyIncidentWorkflowActionCommandHandler> logger)
    : ICommandHandler<ApplyIncidentWorkflowActionCommand>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ApplyIncidentWorkflowActionCommandHandler> _logger = logger;

    public async Task<Result> Handle(ApplyIncidentWorkflowActionCommand request, CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.IncidentId);
            return Result.Failure("Incident not found.");
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure("A linked employee is required.");
        }

        var actor = EventActor.User(_currentUser.GetUserId(), employeeId.Value);
        var now = _dateTimeProvider.Now;

        var targetAlias = request.Action switch
        {
            IncidentWorkflowAction.BeginResponding => ProductStatusAlias.Responding,
            IncidentWorkflowAction.MarkRecovered => ProductStatusAlias.Resolved,
            IncidentWorkflowAction.Close => ProductStatusAlias.Closed,
            IncidentWorkflowAction.Cancel => ProductStatusAlias.Canceled,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        var status = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Incident.Key,
            scopeId: null,
            (int)targetAlias,
            cancellationToken);

        if (status.IsFailure)
        {
            _logger.LogError("Unable to resolve incident status {Alias}. Error: {Error}", targetAlias, status.Error);
            return Result.Failure(status.Error);
        }

        var result = request.Action switch
        {
            IncidentWorkflowAction.BeginResponding => incident.BeginResponding(status.Value, actor, now),
            IncidentWorkflowAction.MarkRecovered => incident.MarkRecovered(status.Value, actor, now),
            IncidentWorkflowAction.Close => incident.Close(status.Value, actor, now),
            IncidentWorkflowAction.Cancel => incident.Cancel(status.Value, actor, now, request.Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        if (result.IsFailure)
        {
            incident.ClearDomainEvents();
            return result;
        }

        await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
