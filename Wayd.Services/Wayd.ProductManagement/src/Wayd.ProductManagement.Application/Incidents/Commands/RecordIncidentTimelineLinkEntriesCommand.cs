using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

/// <summary>One link change to persist on an incident timeline.</summary>
public sealed record IncidentTimelineLinkChangeDto(
    IncidentTimelineLinkKind Kind,
    IncidentTimelineLinkAction Action,
    string Label,
    string? FromLabel = null,
    string? ToLabel = null);

/// <summary>Persists link timeline entries after work item or generic link changes.</summary>
public sealed record RecordIncidentTimelineLinkEntriesCommand(
    Guid IncidentId,
    IReadOnlyList<IncidentTimelineLinkChangeDto> Changes) : ICommand, IRequireLinkedEmployee;

public sealed class RecordIncidentTimelineLinkEntriesCommandValidator
    : AbstractValidator<RecordIncidentTimelineLinkEntriesCommand>
{
    public RecordIncidentTimelineLinkEntriesCommandValidator()
    {
        RuleFor(c => c.IncidentId).NotEmpty();
        RuleForEach(c => c.Changes).ChildRules(change =>
        {
            change.RuleFor(c => c.Label).NotEmpty().MaximumLength(256);
        });
    }
}

public sealed class RecordIncidentTimelineLinkEntriesCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RecordIncidentTimelineLinkEntriesCommandHandler> logger)
    : ICommandHandler<RecordIncidentTimelineLinkEntriesCommand>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RecordIncidentTimelineLinkEntriesCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        RecordIncidentTimelineLinkEntriesCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Changes.Count == 0)
        {
            return Result.Success();
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure("A linked employee is required.");
        }

        var incident = await _productManagementDbContext.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.IncidentId);
            return Result.Failure("Incident not found.");
        }

        var recordedAt = _dateTimeProvider.Now;
        var timestamp = _dateTimeProvider.Now;
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId.Value);
        foreach (var change in request.Changes)
        {
            var recorded = incident.RecordLinkTimelineEntry(
                change.Kind,
                change.Action,
                change.Label,
                change.FromLabel,
                change.ToLabel,
                actor,
                recordedAt,
                timestamp);
            if (recorded.IsFailure)
            {
                return Result.Failure(recorded.Error);
            }
        }

        await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
