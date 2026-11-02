using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.StatusWorkflows.Commands;

/// <summary>
/// Copies a workflow scheme into a new owned one — how a scope that needs to differ gets its own answer:
/// clone, edit the copy, then assign it to the scope.
/// </summary>
public sealed record CloneWorkflowSchemeCommand(Guid Id, string Name, string? Description) : ICommand<Guid>;

public sealed class CloneWorkflowSchemeCommandValidator : AbstractValidator<CloneWorkflowSchemeCommand>
{
    public CloneWorkflowSchemeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
    }
}

public sealed class CloneWorkflowSchemeCommandHandler(
    IStatusWorkflowDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CloneWorkflowSchemeCommandHandler> logger,
    IWorkflowOwnerAvailability ownerAvailability)
    : ICommandHandler<CloneWorkflowSchemeCommand, Guid>
{
    private const string AppRequestName = nameof(CloneWorkflowSchemeCommand);

    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CloneWorkflowSchemeCommandHandler> _logger = logger;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<Guid>> Handle(CloneWorkflowSchemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var source = await _dbContext.StatusWorkflowSchemes
                .Include(s => s.Entries)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (source is null || source.IsImplicit || !await _ownerAvailability.IsAvailable(source.OwnerType, cancellationToken))
            {
                return Result.Failure<Guid>("Workflow scheme not found.");
            }

            var clone = source.Clone(request.Name, request.Description, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (clone.IsFailure)
            {
                return Result.Failure<Guid>(clone.Error);
            }

            _dbContext.StatusWorkflowSchemes.Add(clone.Value);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(clone.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
