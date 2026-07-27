using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Planning.Application.StoryMaps.Commands;

public sealed record ReorderPersonaCommand(Guid StoryMapId, Guid PersonaId, int NewOrder) : ICommand;

public sealed class ReorderPersonaCommandValidator : CustomValidator<ReorderPersonaCommand>
{
    public ReorderPersonaCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.StoryMapId).NotEmpty();
        RuleFor(c => c.PersonaId).NotEmpty();
        RuleFor(c => c.NewOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class ReorderPersonaCommandHandler(IPlanningDbContext planningDbContext, IStoryMapNotifier notifier, ILogger<ReorderPersonaCommandHandler> logger) : ICommandHandler<ReorderPersonaCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IStoryMapNotifier _notifier = notifier;
    private readonly ILogger<ReorderPersonaCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderPersonaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await StoryMapMutation.Apply(
                _planningDbContext,
                ct => _planningDbContext.StoryMaps
                    .Include(m => m.Personas)
                    .FirstOrDefaultAsync(m => m.Id == request.StoryMapId, ct),
                map => map.ReorderPersona(request.PersonaId, request.NewOrder)
                    .Map(() => map.Personas.First(p => p.Id == request.PersonaId).Order),
                cancellationToken);

            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _notifier.NotifyPersonaReordered(request.StoryMapId, request.PersonaId, result.Value);

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
