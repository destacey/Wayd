using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Commands;

public sealed record UpdateRiskCommand(Guid Id, string Summary, string? Description, Guid TeamId,
    RiskStatus Status, RiskCategory Category, RiskGrade Impact, RiskGrade Likelihood, Guid? AssigneeId,
    LocalDate? FollowUpDate, string? Response) : ICommand<int>;

public sealed class UpdateRiskCommandValidator : CustomValidator<UpdateRiskCommand>
{
    public UpdateRiskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamId)
            .NotEmpty();

        RuleFor(r => r.Summary)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Description)
            .MaximumLength(1024);

        RuleFor(r => r.Status)
            .IsInEnum()
            .WithMessage("A valid status must be selected.");

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

public sealed class UpdateRiskCommandHandler : ICommandHandler<UpdateRiskCommand, int>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateRiskCommandHandler> _logger;

    public UpdateRiskCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateRiskCommandHandler> logger)
    {
        _planningDbContext = planningDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(UpdateRiskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var risk = await _planningDbContext.Risks
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.TeamId == request.TeamId, cancellationToken);
            if (risk is null)
                return Result.Failure<int>("Risk not found.");

            var updateResult = risk.Update(
                request.Summary,
                request.Description,
                request.Status,
                request.Category,
                request.Impact,
                request.Likelihood,
                request.AssigneeId,
                request.FollowUpDate,
                request.Response,
                _dateTimeProvider.Now
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(risk).ReloadAsync(cancellationToken);
                risk.ClearDomainEvents();

                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", request.GetType().Name, request, updateResult.Error);
                return Result.Failure<int>(updateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(risk.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
