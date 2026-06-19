using Wayd.Common.Application.FeatureManagement.Commands;

namespace Wayd.Web.Api.Models.Admin;

public class UpdateFeatureFlagRequest
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }

    public UpdateFeatureFlagCommand ToUpdateFeatureFlagCommand()
    {
        return new UpdateFeatureFlagCommand(Id, DisplayName, Description);
    }
}

public sealed class UpdateFeatureFlagRequestValidator : CustomValidator<UpdateFeatureFlagRequest>
{
    public UpdateFeatureFlagRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Id).GreaterThan(0);
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
