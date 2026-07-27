using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record DeleteStoryMapCommand(Guid Id) : ICommand;

public sealed class DeleteStoryMapCommandValidator : CustomValidator<DeleteStoryMapCommand>
{
    public DeleteStoryMapCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteStoryMapCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<DeleteStoryMapCommandHandler> logger) : ICommandHandler<DeleteStoryMapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<DeleteStoryMapCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var map = await _planningDbContext.StoryMaps
                .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

            if (map is null)
                return Result.Failure("Story map not found.");

            // StoryMap is soft-deletable; the DbContext converts the Remove into a soft delete.
            _planningDbContext.StoryMaps.Remove(map);
            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyMapDeleted(request.Id);

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
