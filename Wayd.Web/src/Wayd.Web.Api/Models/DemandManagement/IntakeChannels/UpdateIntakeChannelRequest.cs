using Wayd.DemandManagement.Application.IntakeChannels.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeChannels;

/// <summary>
/// A channel's new name and description. Every request carrying the channel reads the new name.
/// </summary>
public sealed record UpdateIntakeChannelRequest
{
    /// <summary>
    /// The channel being changed; must match the route.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What the company calls the channel. Unique among active and archived channels.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// What belongs in the channel, or null for no description.
    /// </summary>
    public string? Description { get; set; }

    public UpdateIntakeChannelCommand ToUpdateIntakeChannelCommand() => new(Id, Name, Description);
}

public sealed class UpdateIntakeChannelRequestValidator : CustomValidator<UpdateIntakeChannelRequest>
{
    public UpdateIntakeChannelRequestValidator()
    {
        RuleFor(r => r.Id).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Description).MaximumLength(512);
    }
}
