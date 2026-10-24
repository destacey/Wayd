using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following;

/// <summary>
/// Pairs a caller-supplied owner type with its registered descriptor and the read rule that reads it.
/// </summary>
internal static class FollowReadRules
{
    /// <summary>
    /// Resolves an owner type, failing when it is not registered or no module answers for it.
    /// </summary>
    public static Result<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)> Resolve(
        IEnumerable<IRecordReadRule> readRules, string? ownerType)
    {
        var descriptor = FollowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<(FollowOwnerDescriptor, IRecordReadRule)>(descriptor.Error);
        }

        var readRule = readRules.FirstOrDefault(t =>
            string.Equals(t.OwnerType, descriptor.Value.Key, StringComparison.OrdinalIgnoreCase));

        return readRule is null
            ? Result.Failure<(FollowOwnerDescriptor, IRecordReadRule)>(
                $"'{descriptor.Value.Key}' is not a kind of record that can be followed.")
            : (descriptor.Value, readRule);
    }

    /// <summary>
    /// Whether the caller may read the record, through its owner type's own read rule.
    /// </summary>
    public static async Task<bool> CanRead(IRecordReadRule readRule, Guid ownerId, CancellationToken cancellationToken)
    {
        var readable = await readRule.ReadableIds(cancellationToken);

        return readable is not null && await readable.AnyAsync(id => id == ownerId, cancellationToken);
    }
}
