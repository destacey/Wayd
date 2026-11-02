using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

/// <summary>Adds a commander timeline note to an incident.</summary>
public sealed record AddIncidentTimelineNoteCommand(
    Guid IncidentId,
    string Body,
    Instant? RecordedAt) : ICommand<Guid>, IRequireLinkedEmployee;

public sealed class AddIncidentTimelineNoteCommandValidator : AbstractValidator<AddIncidentTimelineNoteCommand>
{
    public AddIncidentTimelineNoteCommandValidator()
    {
        RuleFor(c => c.IncidentId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(4096);
    }
}

public sealed class AddIncidentTimelineNoteCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<AddIncidentTimelineNoteCommandHandler> logger)
    : ICommandHandler<AddIncidentTimelineNoteCommand, Guid>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<AddIncidentTimelineNoteCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(
        AddIncidentTimelineNoteCommand request,
        CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure<Guid>("A linked employee is required.");
        }

        var incident = await _productManagementDbContext.Incidents
            .FirstOrDefaultAsync(i => i.Id == request.IncidentId, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.IncidentId);
            return Result.Failure<Guid>("Incident not found.");
        }

        var recordedAt = request.RecordedAt ?? _dateTimeProvider.Now;
        var added = incident.AddTimelineNote(request.Body, employeeId.Value, recordedAt);
        if (added.IsFailure)
        {
            return Result.Failure<Guid>(added.Error);
        }

        await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(added.Value.Id);
    }
}
