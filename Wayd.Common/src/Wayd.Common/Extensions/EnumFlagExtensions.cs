using System.Numerics;

namespace Wayd.Common.Extensions;

/// <summary>
/// Converts between a <see cref="FlagsAttribute"/> enum, which stores a set as one value, and the collection
/// of members that value names.
/// </summary>
/// <remarks>
/// Flags are a compact way to store a small closed set, but a bitmask is a poor thing to publish: a client
/// would have to know the numbers, and a boundary that serializes enums by name has no name for a
/// combination. Storing the flags and carrying a collection outward keeps both ends honest, and keeps
/// "does this include X" a single containment test wherever it is asked.
/// </remarks>
public static class EnumFlagExtensions
{
    /// <summary>
    /// Whether a value names only declared members, and names at least one unless the enum declares a zero
    /// member of its own.
    /// </summary>
    /// <remarks>
    /// <see cref="Enum.IsDefined{TEnum}(TEnum)"/> cannot answer this: it knows only declared members, so it
    /// rejects every combination of them, which on a flags enum is the ordinary case. Zero is refused unless
    /// the enum names it, because it is what an empty collection converts to — an enum with no zero member
    /// is saying that "none" is not one of its values, and should be stored as null instead.
    /// </remarks>
    public static bool IsValidFlagCombination<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var bits = ToBits(value);

        return (bits & ~FlagInfo<TEnum>.DeclaredMask) == 0 && (bits != 0 || FlagInfo<TEnum>.DeclaresZero);
    }

    /// <summary>
    /// The members a value names, ascending. Only members naming a single bit, so neither a zero member nor
    /// a declared combination such as <c>All</c> appears alongside the members it is made of.
    /// </summary>
    public static IReadOnlyCollection<TEnum> ToFlags<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var bits = ToBits(value);

        return [.. FlagInfo<TEnum>.SingleBitMembers.Where(member => (bits & ToBits(member)) != 0)];
    }

    /// <inheritdoc cref="ToFlags{TEnum}(TEnum)"/>
    /// <returns>Null where nothing is recorded, which an empty collection would not distinguish.</returns>
    public static IReadOnlyCollection<TEnum>? ToFlags<TEnum>(this TEnum? value) where TEnum : struct, Enum =>
        value?.ToFlags();

    /// <summary>
    /// The one value naming every member of a collection, or null for a collection that is null or empty.
    /// </summary>
    /// <remarks>
    /// Empty folds to null so that "nothing recorded" survives a round trip through a boundary with no null
    /// of its own, and so that a caller clearing the collection cannot produce a zero.
    /// </remarks>
    public static TEnum? ToFlagCombination<TEnum>(this IEnumerable<TEnum>? flags) where TEnum : struct, Enum
    {
        if (flags is null)
        {
            return null;
        }

        var bits = flags.Aggregate(0UL, (all, flag) => all | ToBits(flag));

        return bits == 0 ? null : (TEnum)Enum.ToObject(typeof(TEnum), bits);
    }

    private static ulong ToBits<TEnum>(TEnum value) where TEnum : struct, Enum =>
        Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What a flags enum declares, read once per closed type rather than per call.</summary>
    private static class FlagInfo<TEnum> where TEnum : struct, Enum
    {
        public static readonly ulong DeclaredMask =
            Enum.GetValues<TEnum>().Aggregate(0UL, (mask, member) => mask | ToBits(member));

        public static readonly bool DeclaresZero =
            Enum.GetValues<TEnum>().Any(member => ToBits(member) == 0);

        public static readonly TEnum[] SingleBitMembers =
            [.. Enum.GetValues<TEnum>().Where(member => BitOperations.IsPow2(ToBits(member)))];
    }
}
