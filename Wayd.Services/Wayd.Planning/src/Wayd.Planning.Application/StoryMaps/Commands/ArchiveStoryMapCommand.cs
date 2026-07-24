using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ArchiveStoryMapCommand(Guid Id) : ICommand;

public sealed class ArchiveStoryMapCommandValidator : CustomValidator<ArchiveStoryMapCommand>
{
    public ArchiveStoryMapCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class ArchiveStoryMapCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ArchiveStoryMapCommandHandler> logger) : ICommandHandler<ArchiveStoryMapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ArchiveStoryMapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            var result = map.Archive();
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyMapArchived(map.Id);

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
