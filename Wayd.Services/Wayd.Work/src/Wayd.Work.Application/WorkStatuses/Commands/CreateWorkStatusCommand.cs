using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkStatuses.Commands;

public sealed record CreateWorkStatusCommand : ICommand<int>
{
    public CreateWorkStatusCommand(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>The name of the work status.  The name cannot be changed.</summary>
    /// <value>The name.</value>
    public string Name { get; }

    /// <summary>The description of the work status.</summary>
    /// <value>The description.</value>
    public string? Description { get; }
}

public sealed class CreateWorkStatusCommandValidator : CustomValidator<CreateWorkStatusCommand>
{
    private readonly IWorkDbContext _workDbContext;

    public CreateWorkStatusCommandValidator(IWorkDbContext workDbContext)
    {
        _workDbContext = workDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64)
            .MustAsync(BeUniqueName).WithMessage("The work status already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }

    public async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        return await _workDbContext.WorkStatuses
            .AllAsync(e => e.Name != name, cancellationToken);
    }
}

public sealed class CreateWorkStatusCommandHandler : ICommandHandler<CreateWorkStatusCommand, int>
{
    private readonly IWorkDbContext _workDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateWorkStatusCommandHandler> _logger;

    public CreateWorkStatusCommandHandler(IWorkDbContext workDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateWorkStatusCommandHandler> logger)
    {
        _workDbContext = workDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(CreateWorkStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            Instant timestamp = _dateTimeProvider.Now;

            var status = WorkStatus.Create(request.Name, request.Description, timestamp);

            await _workDbContext.WorkStatuses.AddAsync(status, cancellationToken);

            await _workDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(status.Id);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

