namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

public sealed record DeleteIncidentSeveritySetCommand(Guid Id) : ICommand;

public sealed class DeleteIncidentSeveritySetCommandValidator : AbstractValidator<DeleteIncidentSeveritySetCommand>
{
    public DeleteIncidentSeveritySetCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteIncidentSeveritySetCommandHandler(
    IProductManagementDbContext context,
    IIncidentSeverityLevelUsageQuery levelUsage,
    ILogger<DeleteIncidentSeveritySetCommandHandler> logger)
    : ICommandHandler<DeleteIncidentSeveritySetCommand>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly IIncidentSeverityLevelUsageQuery _levelUsage = levelUsage;
    private readonly ILogger<DeleteIncidentSeveritySetCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        DeleteIncidentSeveritySetCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var set = await _context.IncidentSeveritySets
                .Include(s => s.Levels)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (set is null)
            {
                return Result.Failure("Severity set not found.");
            }

            var selectedSetId = await _context.IncidentSeveritySetAssignments
                .Select(a => a.SeveritySetId)
                .SingleAsync(cancellationToken);

            var levelsInUse = await _levelUsage.GetLevelIdsInUse(cancellationToken);
            var levelsInSetInUse = levelsInUse
                .Where(id => set.Levels.Any(l => l.Id == id))
                .ToHashSet();

            var delete = set.Delete(set.Id == selectedSetId, levelsInSetInUse);
            if (delete.IsFailure)
            {
                return delete;
            }

            _context.IncidentSeveritySets.Remove(set);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Incident severity set {SetId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {Command}.", nameof(DeleteIncidentSeveritySetCommand));
            return Result.Failure($"Exception handling {nameof(DeleteIncidentSeveritySetCommand)}.");
        }
    }
}
