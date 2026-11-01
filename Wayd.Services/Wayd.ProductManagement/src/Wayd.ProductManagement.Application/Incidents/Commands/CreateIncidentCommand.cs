using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Commands;

public sealed record CreateIncidentCommand(
    string Title,
    string? Description,
    Guid SeverityLevelId,
    Instant? NoticedAt,
    Instant? StartedAt,
    Instant? RecoveredAt,
    IReadOnlyList<Guid> ProductIds) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).MaximumLength(4096);
        RuleFor(c => c.SeverityLevelId).NotEmpty();
    }
}

public sealed class CreateIncidentCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider,
    ILogger<CreateIncidentCommandHandler> logger)
    : ICommandHandler<CreateIncidentCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateIncidentCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateIncidentCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateIncidentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
            if (employeeId is null)
            {
                return Result.Failure<ObjectIdAndKey>("A linked employee is required to open an incident.");
            }

            var severity = await IncidentReferenceValidation.ValidateSeverityLevelInSelectedSet(
                _productManagementDbContext, request.SeverityLevelId, cancellationToken);
            if (severity.IsFailure)
            {
                return Result.Failure<ObjectIdAndKey>(severity.Error);
            }

            var products = await IncidentReferenceValidation.ValidateProductsExist(
                _productManagementDbContext, request.ProductIds, cancellationToken);
            if (products.IsFailure)
            {
                return Result.Failure<ObjectIdAndKey>(products.Error);
            }

            var openStatus = await _statusResolver.ForAlias(
                ProductWorkflowOwners.Incident.Key,
                scopeId: null,
                (int)ProductStatusAlias.Open,
                cancellationToken);

            if (openStatus.IsFailure)
            {
                _logger.LogError("Unable to resolve the open incident status. Error message: {Error}", openStatus.Error);
                return Result.Failure<ObjectIdAndKey>(openStatus.Error);
            }

            var noticedAt = request.NoticedAt ?? _dateTimeProvider.Now;
            var actor = EventActor.User(_currentUser.GetUserId(), employeeId.Value);

            var incident = Incident.Create(
                request.Title,
                request.Description,
                employeeId.Value,
                request.SeverityLevelId,
                noticedAt,
                request.StartedAt,
                request.RecoveredAt,
                request.ProductIds,
                openStatus.Value,
                actor,
                _dateTimeProvider.Now);

            if (incident.IsFailure)
            {
                return Result.Failure<ObjectIdAndKey>(incident.Error);
            }

            await _productManagementDbContext.Incidents.AddAsync(incident.Value, cancellationToken);
            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Incident {IncidentId} created.", incident.Value.Id);

            return Result.Success(new ObjectIdAndKey(incident.Value.Id, incident.Value.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
