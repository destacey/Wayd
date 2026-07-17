using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record CreateRoadmapCommand(string Name, string? Description, LocalDateRange DateRange, List<Guid> RoadmapManagerIds, Visibility Visibility) : ICommand<ObjectIdAndKey>;

public sealed class CreateRoadmapCommandValidator : AbstractValidator<CreateRoadmapCommand>
{
    private readonly ICurrentUser _currentUser;

    public CreateRoadmapCommandValidator(ICurrentUser currentUser)
    {
        _currentUser = currentUser;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.DateRange)
            .NotNull();

        RuleFor(x => x.RoadmapManagerIds)
            .NotEmpty()
            .Must(IncludeCurrentUser).WithMessage("The current user must be a manager of the Roadmap.");

        RuleForEach(x => x.RoadmapManagerIds)
            .NotEmpty();

        RuleFor(x => x.Visibility)
            .IsInEnum();

        //When(x => x.color != null, () => RuleFor(x => x.color)
        //    .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
        //    .WithMessage("Color must be a valid hex color code."));

        //When(x => x.ParentId.HasValue, () =>
        //{
        //    RuleFor(x => x.ParentId)
        //        .NotEmpty();
        //});
    }

    public bool IncludeCurrentUser(IEnumerable<Guid> roadmapManagerIds)
    {
        var employeeId = Guard.Against.NullOrEmpty(_currentUser.GetEmployeeId());
        return roadmapManagerIds.Contains(employeeId);
    }
}

public sealed class CreateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ILogger<CreateRoadmapCommandHandler> logger) : ICommandHandler<CreateRoadmapCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<CreateRoadmapCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateRoadmapCommand request, CancellationToken cancellationToken)
    {
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
