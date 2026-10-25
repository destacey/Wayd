namespace Wayd.Web.Api.Controllers;

/// <summary>
/// How endpoint descriptions name the kinds of record people follow and are notified about, so every
/// endpoint that takes an owner type lists the same ones.
/// </summary>
internal static class OwnerTypeDocs
{
    /// <summary>The owner types an <c>ownerType</c> parameter accepts.</summary>
    public const string Values =
        "`ownerType` is `ppm.project`, `ppm.program` or `planning.planning-interval-objective`";

    /// <summary><see cref="Values"/>, and what <c>ownerId</c> is.</summary>
    public const string Parameters = $"{Values}; `ownerId` is the record's UUID, not its key.";
}
