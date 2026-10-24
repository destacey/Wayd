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
    /// Every registered owner type that a module answers for, ordered by display name; or just the one
    /// named, failing when it cannot be followed.
    /// </summary>
    public static Result<List<(FollowOwnerDescriptor Descriptor, IRecordReadRule ReadRule)>> Select(
        IEnumerable<IRecordReadRule> readRules, string? ownerType)
    {
        if (ownerType is not null)
        {
            var resolved = Resolve(readRules, ownerType);
            return resolved.IsFailure
                ? Result.Failure<List<(FollowOwnerDescriptor, IRecordReadRule)>>(resolved.Error)
                : new List<(FollowOwnerDescriptor, IRecordReadRule)> { resolved.Value };
        }

        return FollowOwners.All
            .Select(d => Resolve(readRules, d.Key))
            .Where(r => r.IsSuccess)
            .Select(r => r.Value)
            .ToList();
    }

    /// <summary>
    /// Whether the caller may read the record, through its owner type's own read rule.
    /// </summary>
    public static async Task<bool> CanRead(IRecordReadRule readRule, Guid ownerId, CancellationToken cancellationToken)
    {
        var readable = await readRule.Readable(null, cancellationToken);

        return readable is not null && await readable.AnyAsync(r => r.Id == ownerId, cancellationToken);
    }
}
