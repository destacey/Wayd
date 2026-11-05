using Wayd.Common.Domain.Discussions;

namespace Wayd.Common.Application.Discussions;

/// <summary>
/// Pairs a caller-supplied channel type with the module implementation that answers for it.
/// </summary>
internal static class DiscussionChannelTypeResolver
{
    /// <summary>
    /// Resolves a channel type, failing when it is not registered or no module answers for it. The returned
    /// implementation's <see cref="IDiscussionChannelType.ChannelType"/> is the registered spelling.
    /// </summary>
    public static Result<IDiscussionChannelType> Resolve(IEnumerable<IDiscussionChannelType> channelTypes, string? channelType)
    {
        var descriptor = DiscussionChannelTypes.Resolve(channelType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<IDiscussionChannelType>(descriptor.Error);
        }

        var implementation = channelTypes.FirstOrDefault(t =>
            string.Equals(t.ChannelType, descriptor.Value.Key, StringComparison.OrdinalIgnoreCase));

        return implementation is null
            ? Result.Failure<IDiscussionChannelType>(DiscussionErrors.UnknownChannelType(descriptor.Value.Key))
            : Result.Success(implementation);
    }
}
