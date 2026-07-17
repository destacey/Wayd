using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Commands;

public sealed record CreateRiskCommand(string Summary, string? Description, Guid TeamId,
    RiskCategory Category, RiskGrade Impact, RiskGrade Likelihood, Guid? AssigneeId,
    LocalDate? FollowUpDate, string? Response) : ICommand<ObjectIdAndKey>;

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

public sealed class CreateRiskCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ILogger<CreateRiskCommandHandler> logger, ICurrentUser currentUser) : ICommandHandler<CreateRiskCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreateRiskCommandHandler> _logger = logger;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());

    public async Task<Result<ObjectIdAndKey>> Handle(CreateRiskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var risk = Risk.Create(
                request.Summary,
                request.Description,
                request.TeamId,
                _dateTimeProvider.Now,
                _currentUserEmployeeId,
                request.Category,
                request.Impact,
                request.Likelihood,
                request.AssigneeId,
                request.FollowUpDate,
                request.Response
                );

            await _planningDbContext.Risks.AddAsync(risk, cancellationToken);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return new ObjectIdAndKey(risk.Id, risk.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<ObjectIdAndKey>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
