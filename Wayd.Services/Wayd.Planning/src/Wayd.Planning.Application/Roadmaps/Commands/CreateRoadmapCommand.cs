using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record CreateRoadmapCommand(string Name, string? Description, LocalDateRange DateRange, List<Guid> RoadmapManagerIds, Visibility Visibility) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CreateRoadmapCommandValidator : AbstractValidator<CreateRoadmapCommand>
{
    private readonly ICurrentPrincipal _currentPrincipal;

    public CreateRoadmapCommandValidator(ICurrentPrincipal currentPrincipal)
    {
        _currentPrincipal = currentPrincipal;

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

        //When(x => x.color != null, () => RuleFor(x => x.color).IsHexColor());

        //When(x => x.ParentId.HasValue, () =>
        //{
        //    RuleFor(x => x.ParentId)
        //        .NotEmpty();
        //});
    }

    // Resolved rather than read from the token claim, which is a snapshot taken at sign-in: an admin
    // who links a user mid-session would otherwise see this throw. LinkedEmployeeMiddleware has already
    // rejected callers with no link, so a null here means the resolve raced a concurrent unlink — treat
    // it as "not a manager" and let validation report it, rather than throwing.
    public async Task<bool> IncludeCurrentUser(IEnumerable<Guid> roadmapManagerIds, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        return employeeId.HasValue && roadmapManagerIds.Contains(employeeId.Value);
    }
}

public sealed class CreateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<CreateRoadmapCommandHandler> logger) : ICommandHandler<CreateRoadmapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CreateRoadmapCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateRoadmapCommand request, CancellationToken cancellationToken)
    {
        // Enforced here, not only in the validator: Wolverine's code generation requires handlers to
        // be public, so this can be constructed and called directly, skipping both the validator and
        // LinkedEmployeeMiddleware. Outside the try because the catch-all below would turn a refusal
        // into a generic failure.
        var currentUserEmployeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (currentUserEmployeeId is null)
            LinkedEmployeeRequired.Throw();

        // Likewise the creator-is-a-manager rule: the domain accepts whatever manager ids it is
        // given, so a direct call could otherwise create a roadmap the caller cannot administer.
        if (!request.RoadmapManagerIds.Contains(currentUserEmployeeId.Value))
            return Result.Failure<ObjectIdAndKey>("The current user must be a manager of the Roadmap.");

        try
        {
            var result = Roadmap.Create(
                request.Name,
                request.Description,
                request.DateRange,
                request.Visibility,
                request.RoadmapManagerIds
                );

            if (result.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", request.GetType().Name, request, result.Error);
                return Result.Failure<ObjectIdAndKey>(result.Error);
            }

            await _planningDbContext.Roadmaps.AddAsync(result.Value, cancellationToken);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(result.Value.Id, result.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
