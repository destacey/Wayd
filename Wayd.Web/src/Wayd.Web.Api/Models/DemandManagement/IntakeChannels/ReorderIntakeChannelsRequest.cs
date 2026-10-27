using Wayd.DemandManagement.Application.IntakeChannels.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeChannels;

/// <summary>
/// The order requesters see the channels in.
/// </summary>
public sealed record ReorderIntakeChannelsRequest
{
    /// <summary>
    /// Every channel, archived ones included, in the order they should be presented. A partial list is refused.
    /// </summary>
    public List<Guid> OrderedChannelIds { get; set; } = [];

    public ReorderIntakeChannelsCommand ToReorderIntakeChannelsCommand() => new(OrderedChannelIds);
}

public sealed class ReorderIntakeChannelsRequestValidator : CustomValidator<ReorderIntakeChannelsRequest>
{
    public ReorderIntakeChannelsRequestValidator()
    {
        RuleFor(r => r.OrderedChannelIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("The order cannot contain an empty id.");
    }
}
