namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

public sealed record SetIncidentSeveritySetActiveStatusCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetIncidentSeveritySetActiveStatusCommandValidator : AbstractValidator<SetIncidentSeveritySetActiveStatusCommand>
{
    public SetIncidentSeveritySetActiveStatusCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class SetIncidentSeveritySetActiveStatusCommandHandler(
    IProductManagementDbContext context,
    ILogger<SetIncidentSeveritySetActiveStatusCommandHandler> logger)
    : ICommandHandler<SetIncidentSeveritySetActiveStatusCommand>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly ILogger<SetIncidentSeveritySetActiveStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        SetIncidentSeveritySetActiveStatusCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var set = await _context.IncidentSeveritySets
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (set is null)
            {
                return Result.Failure("Severity set not found.");
            }

            var selectedSetId = await _context.IncidentSeveritySetAssignments
                .Select(a => a.SeveritySetId)
                .SingleAsync(cancellationToken);

            var isSelected = set.Id == selectedSetId;

            Result result = request.IsActive
                ? set.Activate()
                : set.Deactivate(isSelected);

            if (result.IsFailure)
            {
                return result;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Incident severity set {SetId} active status set to {IsActive}.",
                request.Id,
                request.IsActive);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {Command}.", nameof(SetIncidentSeveritySetActiveStatusCommand));
            return Result.Failure($"Exception handling {nameof(SetIncidentSeveritySetActiveStatusCommand)}.");
        }
    }
}
