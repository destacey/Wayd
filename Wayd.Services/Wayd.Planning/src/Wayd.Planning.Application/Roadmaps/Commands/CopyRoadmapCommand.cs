using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record CopyRoadmapCommand(Guid SourceRoadmapId, string Name, List<Guid> OwnerIds, Visibility Visibility) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CopyRoadmapCommandValidator : AbstractValidator<CopyRoadmapCommand>
{
    private readonly ICurrentPrincipal _currentPrincipal;

    public CopyRoadmapCommandValidator(ICurrentPrincipal currentPrincipal)
    {
        _currentPrincipal = currentPrincipal;

        RuleFor(x => x.SourceRoadmapId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.OwnerIds)
            .NotEmpty()
            .MustAsync(IncludeCurrentUser).WithMessage("You must be one of the roadmap's owners.");

        RuleForEach(x => x.OwnerIds)
            .NotEmpty();

        RuleFor(x => x.Visibility)
            .IsInEnum();
    }

    // Resolved rather than read from the token claim (a sign-in snapshot); see CreateRoadmapCommand.
    public async Task<bool> IncludeCurrentUser(IEnumerable<Guid> ownerIds, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        return employeeId.HasValue && ownerIds.Contains(employeeId.Value);
    }
}

public sealed class CopyRoadmapCommandHandler(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CopyRoadmapCommandHandler> logger) : ICommandHandler<CopyRoadmapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CopyRoadmapCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CopyRoadmapCommand request, CancellationToken cancellationToken)
    {
        // Outside the try: this is a refusal, and the catch-all below would turn it into a
        // generic failure, losing both the 403 and its explanation.
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        // Likewise the creator-is-an-owner rule: Copy accepts whatever owner ids it is given, so a
        // direct call could otherwise produce a roadmap the caller cannot manage.
        if (!request.OwnerIds.Contains(caller.EmployeeId))
            return Result.Failure<ObjectIdAndKey>("You must be one of the roadmap's owners.");

        try
        {
            var sourceRoadmap = await _planningDbContext.Roadmaps
                .Include(r => r.Items)
                .Where(r => r.Id == request.SourceRoadmapId)
                .Where(Roadmap.AccessibleTo(caller.EmployeeId, caller.IsAdministrator))
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceRoadmap is null)
            {
                return Result.Failure<ObjectIdAndKey>("Source roadmap not found or you do not have permission to view it.");
            }

            // Copy the roadmap
            var copyResult = sourceRoadmap.Copy(request.Name, request.OwnerIds, request.Visibility, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);

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
