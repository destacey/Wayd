using Wayd.Common.Application.FeatureManagement.Commands;

namespace Wayd.Web.Api.Models.Admin;

public class CreateFeatureFlagRequest
{
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }

    public CreateFeatureFlagCommand ToCreateFeatureFlagCommand()
    {
        return new CreateFeatureFlagCommand(Name, DisplayName, Description, IsEnabled);
    }
}

public sealed class CreateFeatureFlagRequestValidator : CustomValidator<CreateFeatureFlagRequest>
{
    public CreateFeatureFlagRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(r => r.Description).MaximumLength(1024);
    }
}
