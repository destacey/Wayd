using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record RenameStepCommand(Guid StoryMapId, Guid StepId, string Name) : ICommand;

public sealed class RenameStepCommandValidator : CustomValidator<RenameStepCommand>
{
    public RenameStepCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();

        RuleFor(c => c.StepId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class RenameStepCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<RenameStepCommandHandler> logger) : ICommandHandler<RenameStepCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<RenameStepCommandHandler> _logger = logger;

    public async Task<Result> Handle(RenameStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .Include(m => m.Goals).ThenInclude(g => g.Steps)
                .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.RenameStep(request.StepId, request.Name);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyStepRenamed(map.Id, request.StepId, request.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
