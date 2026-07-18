using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record CopyRoadmapCommand(Guid SourceRoadmapId, string Name, List<Guid> RoadmapManagerIds, Visibility Visibility) : ICommand<ObjectIdAndKey>;

public sealed class CopyRoadmapCommandValidator : AbstractValidator<CopyRoadmapCommand>
{
    private readonly ICurrentUser _currentUser;

    public CopyRoadmapCommandValidator(ICurrentUser currentUser)
    {
        _currentUser = currentUser;

        RuleFor(x => x.SourceRoadmapId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.RoadmapManagerIds)
            .NotEmpty()
            .Must(IncludeCurrentUser).WithMessage("The current user must be a manager of the Roadmap.");

        RuleForEach(x => x.RoadmapManagerIds)
            .NotEmpty();

        RuleFor(x => x.Visibility)
            .IsInEnum();
    }

    public bool IncludeCurrentUser(IEnumerable<Guid> roadmapManagerIds)
    {
        var employeeId = Guard.Against.NullOrEmpty(_currentUser.GetEmployeeId());
        return roadmapManagerIds.Contains(employeeId);
    }
}

public sealed class CopyRoadmapCommandHandler(
    IPlanningDbContext planningDbContext,
    ICurrentUser currentUser,
    ILogger<CopyRoadmapCommandHandler> logger) : ICommandHandler<CopyRoadmapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<CopyRoadmapCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CopyRoadmapCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var publicVisibility = Visibility.Public;

            // Get the source roadmap - user must have visibility to it (either public or a manager)
            var sourceRoadmap = await _planningDbContext.Roadmaps
                .Include(r => r.Items)
                .Where(r => r.Id == request.SourceRoadmapId)
                .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == _currentUserEmployeeId))
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceRoadmap is null)
            {
                return Result.Failure<ObjectIdAndKey>("Source roadmap not found or you do not have permission to view it.");
            }

            // Copy the roadmap
            var copyResult = sourceRoadmap.Copy(request.Name, request.RoadmapManagerIds, request.Visibility);

            if (copyResult.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}",
                    request.GetType().Name, request, copyResult.Error);
                return Result.Failure<ObjectIdAndKey>(copyResult.Error);
            }

            await _planningDbContext.Roadmaps.AddAsync(copyResult.Value, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(copyResult.Value.Id, copyResult.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
