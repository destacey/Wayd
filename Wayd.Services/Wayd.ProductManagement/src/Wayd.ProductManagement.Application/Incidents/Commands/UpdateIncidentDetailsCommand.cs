using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

public sealed record UpdateIncidentDetailsCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid SeverityLevelId,
    Instant NoticedAt,
    Instant? StartedAt,
    Instant? RecoveredAt,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<Guid>? ReleaseIds = null) : ICommand;

public sealed class UpdateIncidentDetailsCommandValidator : AbstractValidator<UpdateIncidentDetailsCommand>
{
    public UpdateIncidentDetailsCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).MaximumLength(4096);
        RuleFor(c => c.SeverityLevelId).NotEmpty();
    }
}

public sealed class UpdateIncidentDetailsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdateIncidentDetailsCommandHandler> logger)
    : ICommandHandler<UpdateIncidentDetailsCommand>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateIncidentDetailsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateIncidentDetailsCommand request, CancellationToken cancellationToken)
    {
        var incident = await _productManagementDbContext.Incidents
            .Include(i => i.ProductLinks)
            .Include(i => i.ReleaseLinks)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (incident is null)
        {
            _logger.LogInformation("Incident {IncidentId} not found.", request.Id);
            return Result.Failure("Incident not found.");
        }

        var severity = await IncidentReferenceValidation.ValidateSeverityLevelInSelectedSet(
            _productManagementDbContext, request.SeverityLevelId, cancellationToken);
        if (severity.IsFailure)
        {
            return severity;
        }

        var products = await IncidentReferenceValidation.ValidateProductsExist(
            _productManagementDbContext, request.ProductIds, cancellationToken);
        if (products.IsFailure)
        {
            return products;
        }

        var releases = await IncidentReferenceValidation.ValidateReleasesExist(
            _productManagementDbContext, request.ReleaseIds ?? [], cancellationToken);
        if (releases.IsFailure)
        {
            return releases;
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Failure("A linked employee is required.");
        }

        var actor = EventActor.User(_currentUser.GetUserId(), employeeId.Value);
        var timestamp = _dateTimeProvider.Now;

        var updated = incident.UpdateDetails(
            request.Title,
            request.Description,
            request.SeverityLevelId,
            request.NoticedAt,
            request.StartedAt,
            request.RecoveredAt,
            request.ProductIds,
            actor,
            timestamp,
            request.ReleaseIds);

        if (updated.IsFailure)
        {
            return updated;
        }

        await _productManagementDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
