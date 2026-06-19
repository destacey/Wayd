using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;

namespace Wayd.Web.Api.Models.Planning.HealthChecks;

public class UpdatePlanningIntervalObjectiveHealthCheckRequest
{
    public Guid PlanningIntervalObjectiveId { get; set; }
    public Guid Id { get; set; }
    public int StatusId { get; set; }
    public Instant Expiration { get; set; }
    public string? Note { get; set; }

    public UpdatePlanningIntervalObjectiveHealthCheckCommand ToCommand()
        => new(PlanningIntervalObjectiveId, Id, (HealthStatus)StatusId, Expiration, Note);
}

public sealed class UpdatePlanningIntervalObjectiveHealthCheckRequestValidator
    : CustomValidator<UpdatePlanningIntervalObjectiveHealthCheckRequest>
{
    public UpdatePlanningIntervalObjectiveHealthCheckRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.PlanningIntervalObjectiveId)
            .NotEmpty();

        RuleFor(r => r.Id)
            .NotEmpty();

        RuleFor(r => (HealthStatus)r.StatusId)
            .IsInEnum()
            .WithMessage("A valid health status must be selected.");

        RuleFor(r => r.Expiration)
            .NotEmpty()
            .GreaterThan(dateTimeProvider.Now)
            .WithMessage("The Expiration must be in the future.");

        RuleFor(r => r.Note)
            .MaximumLength(1024);
    }
}
