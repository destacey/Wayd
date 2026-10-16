using Wayd.Planning.Application.PlanningIntervals.Commands;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

public sealed record UpdatePlanningIntervalRequest
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets the team name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Gets the team description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the objectives locked.</summary>
    public bool ObjectivesLocked { get; set; }

    public UpdatePlanningIntervalCommand ToUpdatePlanningIntervalCommand()
    {
        return new UpdatePlanningIntervalCommand(Id, Name, Description, ObjectivesLocked);
    }
}

public sealed class UpdatePlanningIntervalRequestValidator : CustomValidator<UpdatePlanningIntervalRequest>
{
    public UpdatePlanningIntervalRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);
    }
}
