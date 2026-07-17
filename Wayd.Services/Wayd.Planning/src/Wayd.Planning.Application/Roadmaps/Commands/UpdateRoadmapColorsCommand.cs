using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapColorsCommand(Guid RoadmapId, List<UpsertRoadmapColorModel> Colors) : ICommand;

public sealed record UpsertRoadmapColorModel(string Color, string Name, int Order, bool IsDefault) : IUpsertRoadmapColor;

public sealed class UpdateRoadmapColorsCommandValidator : AbstractValidator<UpdateRoadmapColorsCommand>
{
    public UpdateRoadmapColorsCommandValidator()
    {
        RuleFor(x => x.RoadmapId)
            .NotEmpty();

        RuleFor(x => x.Colors)
            .NotNull()
            .Must(colors => colors.Count <= Roadmap.MaxColors)
                .WithMessage($"A Roadmap cannot have more than {Roadmap.MaxColors} colors.")
            .Must(colors => colors.Count(c => c.IsDefault) <= 1)
                .WithMessage("Only one color can be marked as the default.");

        RuleForEach(x => x.Colors).ChildRules(color =>
        {
            color.RuleFor(c => c.Color)
                .NotEmpty()
                .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
                    .WithMessage("Color must be a valid hex color code.");

            color.RuleFor(c => c.Name)
                .NotEmpty()
                .MaximumLength(32);
        });
    }
}

public sealed class UpdateRoadmapColorsCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapColorsCommandHandler> logger) : ICommandHandler<UpdateRoadmapColorsCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapColorsCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapColorsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapColorsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
            {
                _logger.LogInformation("Roadmap with id {RoadmapId} not found.", request.RoadmapId);
                return Result.Failure($"Roadmap with id {request.RoadmapId} not found");
            }

            var updateResult = roadmap.UpdateColors(request.Colors, _currentUserEmployeeId);

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);
                roadmap.ClearDomainEvents();

                _logger.LogError("Unable to update colors for Roadmap {RoadmapId}.  Error message: {Error}", request.RoadmapId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Colors for Roadmap {RoadmapId} updated.", request.RoadmapId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
