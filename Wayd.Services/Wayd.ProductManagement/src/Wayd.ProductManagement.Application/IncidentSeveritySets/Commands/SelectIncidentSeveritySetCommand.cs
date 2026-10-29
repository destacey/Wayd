namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

public sealed record SelectIncidentSeveritySetCommand(Guid SeveritySetId) : ICommand;

public sealed class SelectIncidentSeveritySetCommandValidator : AbstractValidator<SelectIncidentSeveritySetCommand>
{
    public SelectIncidentSeveritySetCommandValidator()
    {
        RuleFor(c => c.SeveritySetId).NotEmpty();
    }
}

public sealed class SelectIncidentSeveritySetCommandHandler(
    IProductManagementDbContext context,
    ILogger<SelectIncidentSeveritySetCommandHandler> logger)
    : ICommandHandler<SelectIncidentSeveritySetCommand>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly ILogger<SelectIncidentSeveritySetCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        SelectIncidentSeveritySetCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var set = await _context.IncidentSeveritySets
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SeveritySetId, cancellationToken);

            if (set is null)
            {
                return Result.Failure("Severity set not found.");
            }

            var assignment = await _context.IncidentSeveritySetAssignments.SingleAsync(cancellationToken);
            var result = assignment.Select(request.SeveritySetId, set.IsActive);
            if (result.IsFailure)
            {
                return result;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Organization incident severity set changed to {SetId}.",
                request.SeveritySetId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {Command}.", nameof(SelectIncidentSeveritySetCommand));
            return Result.Failure($"Exception handling {nameof(SelectIncidentSeveritySetCommand)}.");
        }
    }
}
