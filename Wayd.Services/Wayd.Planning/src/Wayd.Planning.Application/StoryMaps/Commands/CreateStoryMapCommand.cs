using Wayd.Common.Application.Models;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record CreateStoryMapCommand(string Name, string? Description) : ICommand<ObjectIdAndKey>;

public sealed class CreateStoryMapCommandValidator : CustomValidator<CreateStoryMapCommand>
{
    public CreateStoryMapCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(2048);
    }
}

public sealed class CreateStoryMapCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<CreateStoryMapCommandHandler> logger) : ICommandHandler<CreateStoryMapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<CreateStoryMapCommandHandler> _logger = logger;
    private readonly string _currentUserId = currentUser.GetUserId();

    public async Task<Result<ObjectIdAndKey>> Handle(CreateStoryMapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var mapResult = StoryMap.Create(
                request.Name,
                request.Description,
                _currentUserId);

            if (mapResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(mapResult.Error);

            await _planningDbContext.StoryMaps.AddAsync(mapResult.Value, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(mapResult.Value.Id, mapResult.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
