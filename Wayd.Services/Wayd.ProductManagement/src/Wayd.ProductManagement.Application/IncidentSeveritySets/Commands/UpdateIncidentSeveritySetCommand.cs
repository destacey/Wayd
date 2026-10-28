namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

public sealed record IncidentSeveritySetLevelPayload(Guid? Id, string Name);

public sealed record UpdateIncidentSeveritySetCommand(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<IncidentSeveritySetLevelPayload> Levels) : ICommand;

public sealed class UpdateIncidentSeveritySetCommandValidator : AbstractValidator<UpdateIncidentSeveritySetCommand>
{
    public UpdateIncidentSeveritySetCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
        RuleFor(c => c.Levels).NotEmpty();
        RuleForEach(c => c.Levels).ChildRules(level =>
        {
            level.RuleFor(l => l.Name).NotEmpty().MaximumLength(64);
        });
    }
}

public sealed class UpdateIncidentSeveritySetCommandHandler(
    IProductManagementDbContext context,
    IIncidentSeverityLevelUsageQuery levelUsage,
    ILogger<UpdateIncidentSeveritySetCommandHandler> logger)
    : ICommandHandler<UpdateIncidentSeveritySetCommand>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly IIncidentSeverityLevelUsageQuery _levelUsage = levelUsage;
    private readonly ILogger<UpdateIncidentSeveritySetCommandHandler> _logger = logger;

    public async Task<Result> Handle(
        UpdateIncidentSeveritySetCommand request,
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

            var name = request.Name.Trim();
            if (await _context.IncidentSeveritySets
                    .AnyAsync(s => s.Id != request.Id && s.Name == name, cancellationToken))
            {
                return Result.Failure($"A severity set named '{name}' already exists.");
            }

            var details = set.UpdateDetails(name, request.Description);
            if (details.IsFailure)
            {
                return details;
            }

            var levelsInUse = await _levelUsage.GetLevelIdsInUse(cancellationToken);
            var replace = set.ReplaceLevels(
                request.Levels.Select(l => (l.Id, l.Name.Trim())).ToList(),
                levelsInUse);

            if (replace.IsFailure)
            {
                return replace;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Incident severity set {SetId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {Command}.", nameof(UpdateIncidentSeveritySetCommand));
            return Result.Failure($"Exception handling {nameof(UpdateIncidentSeveritySetCommand)}.");
        }
    }
}
