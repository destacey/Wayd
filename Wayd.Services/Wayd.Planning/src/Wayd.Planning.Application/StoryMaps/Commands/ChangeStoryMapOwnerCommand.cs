using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ChangeStoryMapOwnerCommand(Guid Id, string OwnerId) : ICommand;

public sealed class ChangeStoryMapOwnerCommandValidator : CustomValidator<ChangeStoryMapOwnerCommand>
{
    public ChangeStoryMapOwnerCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}

public sealed class ChangeStoryMapOwnerCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ChangeStoryMapOwnerCommandHandler> logger) : ICommandHandler<ChangeStoryMapOwnerCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ChangeStoryMapOwnerCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeStoryMapOwnerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var ownerExists = await _planningDbContext.WaydUsers
                .AnyAsync(u => u.Id == request.OwnerId, cancellationToken);

            if (!ownerExists)
                return Result.Failure("The specified owner does not exist.");

            var map = await _planningDbContext.StoryMaps
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.ChangeOwner(request.OwnerId);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyMapUpdated(map.Id);

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
