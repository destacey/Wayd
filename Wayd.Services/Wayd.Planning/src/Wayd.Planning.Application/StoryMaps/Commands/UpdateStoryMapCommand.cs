using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record UpdateStoryMapCommand(Guid Id, string Name, string? Description) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateStoryMapCommandValidator : CustomValidator<UpdateStoryMapCommand>
{
    public UpdateStoryMapCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(2048);
    }
}

public sealed class UpdateStoryMapCommandHandler(IPlanningDbContext planningDbContext, IStoryMapAccess access, IStoryMapNotifier notifier, ILogger<UpdateStoryMapCommandHandler> logger) : ICommandHandler<UpdateStoryMapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapAccess _access = access;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<UpdateStoryMapCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .IncludeAccess()
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var actor = await _access.ResolveActor(map, cancellationToken);

            var result = map.Update(request.Name, request.Description, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyMapUpdated(map.Id);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
