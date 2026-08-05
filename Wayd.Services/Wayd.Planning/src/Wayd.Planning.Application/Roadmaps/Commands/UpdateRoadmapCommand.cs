using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapCommand(Guid Id, string Name, string? Description, LocalDateRange DateRange, List<Guid> RoadmapManagerIds, Visibility Visibility) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateRoadmapCommandValidator : AbstractValidator<UpdateRoadmapCommand>
{
    private readonly ICurrentPrincipal _currentPrincipal;

    public UpdateRoadmapCommandValidator(ICurrentPrincipal currentPrincipal)
    {
        _currentPrincipal = currentPrincipal;

        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.DateRange)
            .NotNull();

        RuleFor(x => x.RoadmapManagerIds)
            .NotEmpty()
            .MustAsync(IncludeCurrentUser).WithMessage("The current user must be a manager of the Roadmap.");

        RuleForEach(x => x.RoadmapManagerIds)
            .NotEmpty();

        RuleFor(x => x.Visibility)
            .IsInEnum();
    }

    // Resolved rather than read from the token claim (a sign-in snapshot); see CreateRoadmapCommand.
    public async Task<bool> IncludeCurrentUser(IEnumerable<Guid> roadmapManagerIds, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        return employeeId.HasValue && roadmapManagerIds.Contains(employeeId.Value);
    }
}

public sealed class UpdateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<UpdateRoadmapCommandHandler> logger) : ICommandHandler<UpdateRoadmapCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<UpdateRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapCommand request, CancellationToken cancellationToken)
    {
        // Outside the try: this is a refusal, and the catch-all below would turn it into a
        // generic failure, losing both the 403 and its explanation.
        var currentUserEmployeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (currentUserEmployeeId is null)
            LinkedEmployeeRequired.Throw();

        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (roadmap is null)
            {
                _logger.LogInformation("Roadmap with id {RoadmapId} not found.", request.Id);
                return Result.Failure($"Roadmap with id {request.Id} not found");
            }

            var updateResult = roadmap.Update(
                request.Name,
                request.Description,
                request.DateRange,
                request.RoadmapManagerIds,
                request.Visibility,
                currentUserEmployeeId.Value
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);
                roadmap.ClearDomainEvents();

                _logger.LogError("Unable to update Roadmap {RoadmapId}.  Error message: {Error}", request.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Roadmap {RoadmapId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
