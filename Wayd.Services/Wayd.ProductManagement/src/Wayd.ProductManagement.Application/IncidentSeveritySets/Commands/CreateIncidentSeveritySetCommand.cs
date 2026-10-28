namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;

public sealed record CreateIncidentSeveritySetCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> LevelNames) : ICommand<Guid>;

public sealed class CreateIncidentSeveritySetCommandValidator : AbstractValidator<CreateIncidentSeveritySetCommand>
{
    public CreateIncidentSeveritySetCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
        RuleFor(c => c.LevelNames).NotEmpty();
        RuleForEach(c => c.LevelNames).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateIncidentSeveritySetCommandHandler(
    IProductManagementDbContext context,
    ILogger<CreateIncidentSeveritySetCommandHandler> logger)
    : ICommandHandler<CreateIncidentSeveritySetCommand, Guid>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly ILogger<CreateIncidentSeveritySetCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(
        CreateIncidentSeveritySetCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var name = request.Name.Trim();
            if (await _context.IncidentSeveritySets.AnyAsync(s => s.Name == name, cancellationToken))
            {
                return Result.Failure<Guid>($"A severity set named '{name}' already exists.");
            }

            var create = IncidentSeveritySet.CreateCustom(name, request.Description, request.LevelNames);
            if (create.IsFailure)
            {
                return Result.Failure<Guid>(create.Error);
            }

            _context.IncidentSeveritySets.Add(create.Value);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Incident severity set {SetId} created.", create.Value.Id);

            return Result.Success(create.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {Command}.", nameof(CreateIncidentSeveritySetCommand));
            return Result.Failure<Guid>($"Exception handling {nameof(CreateIncidentSeveritySetCommand)}.");
        }
    }
}
