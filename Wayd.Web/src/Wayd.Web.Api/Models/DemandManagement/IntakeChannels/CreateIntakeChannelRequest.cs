using Wayd.DemandManagement.Application.IntakeChannels.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeChannels;

/// <summary>
/// A channel to add to the end of the company's list of where requests come from.
/// </summary>
public sealed record CreateIntakeChannelRequest
{
    /// <summary>
    /// What the company calls the channel. Unique among active and archived channels.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What belongs in the channel, shown to whoever picks one.
    /// </summary>
    public string? Description { get; set; }

    public CreateIntakeChannelCommand ToCreateIntakeChannelCommand() => new(Name, Description);
}

public sealed class CreateIntakeChannelRequestValidator : CustomValidator<CreateIntakeChannelRequest>
{
    public CreateIntakeChannelRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
