using Wayd.Planning.Application.Risks.Commands;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Web.Api.Models.Planning.Risks;

public class CreateRiskRequest
{
    public Guid TeamId { get; set; }
    public string Summary { get; set; } = default!;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public int ImpactId { get; set; }
    public int LikelihoodId { get; set; }
    public Guid? AssigneeId { get; set; }
    public LocalDate? FollowUpDate { get; set; }
    public string? Response { get; set; }

    public CreateRiskCommand ToCreateRiskCommand()
    {
        return new CreateRiskCommand(Summary, Description, TeamId, (RiskCategory)CategoryId, (RiskGrade)ImpactId, (RiskGrade)LikelihoodId, AssigneeId, FollowUpDate, Response);
    }
}

public sealed class CreateRiskRequestValidator : CustomValidator<CreateRiskRequest>
{
    public CreateRiskRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamId)
            .NotEmpty();

        RuleFor(r => r.Summary)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Description)
            .MaximumLength(1024);

        RuleFor(r => (RiskCategory)r.CategoryId)
            .IsInEnum()
            .WithMessage("A valid category must be selected.");

        RuleFor(r => (RiskGrade)r.ImpactId)
            .IsInEnum()
            .WithMessage("A valid impact must be selected.");

        RuleFor(r => (RiskGrade)r.LikelihoodId)
            .IsInEnum()
            .WithMessage("A valid likelihood must be selected.");

        RuleFor(r => r.Response)
            .MaximumLength(1024);
    }
}
