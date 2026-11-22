using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Risks.Commands;

public sealed record CreateRiskCommand(string Summary, string? Description, Guid TeamId,
    RiskCategory Category, RiskGrade Impact, RiskGrade Likelihood, Guid? AssigneeId,
    LocalDate? FollowUpDate, string? Response) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CreateRiskCommandValidator : CustomValidator<CreateRiskCommand>
{
    public CreateRiskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamId)
            .NotEmpty();

        RuleFor(r => r.Summary)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Description)
            .MaximumLength(1024);

        RuleFor(r => r.Category)
            .IsInEnum()
            .WithMessage("A valid category must be selected.");

        RuleFor(r => r.Impact)
            .IsInEnum()
            .WithMessage("A valid impact must be selected.");

        RuleFor(r => r.Likelihood)
            .IsInEnum()
            .WithMessage("A valid likelihood must be selected.");

        RuleFor(r => r.Response)
            .MaximumLength(1024);
    }
}

public sealed class CreateRiskCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser) : ICommandHandler<CreateRiskCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateRiskCommand request, CancellationToken cancellationToken)
    {
        var currentUserEmployeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (currentUserEmployeeId is null)
            LinkedEmployeeRequired.Throw();

        // The team and assignee are foreign keys, so an unknown one is refused here rather than at the database.
        var exist = Result.Combine(
            await _planningDbContext.EnsureTeamsExist([request.TeamId], cancellationToken),
            await _planningDbContext.EnsureEmployeesExist(request.AssigneeId is { } assigneeId ? [assigneeId] : [], cancellationToken));
        if (exist.IsFailure)
            return Result.Failure<ObjectIdAndKey>(exist.Error);

        var now = _dateTimeProvider.Now;

        var risk = Risk.Create(
            request.Summary,
            request.Description,
            request.TeamId,
            now,
            currentUserEmployeeId.Value,
            request.Category,
            request.Impact,
            request.Likelihood,
            request.AssigneeId,
            request.FollowUpDate,
            request.Response,
            EventActor.User(_currentUser.GetUserId(), currentUserEmployeeId.Value),
            now
            );

        await _planningDbContext.Risks.AddAsync(risk, cancellationToken);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return new ObjectIdAndKey(risk.Id, risk.Key);
    }
}
