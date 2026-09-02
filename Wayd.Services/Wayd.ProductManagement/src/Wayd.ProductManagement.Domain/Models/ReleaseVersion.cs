namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One version carried directly by a <see cref="Release"/>, outside any package.
/// </summary>
/// <remarks>
/// The single-artifact announcement: <c>@wayd/mcp 1.2.0</c> shipped on its own and was announced on
/// its own. Routing it through a package of one would invent a deployment unit nobody assembled, and
/// that phantom package would then appear in the packages list and in deployment-unit reasoning.
/// <para>
/// A version reachable through one of the release's packages must not also appear here — that is
/// <see cref="Release.CarryVersions"/>'s invariant, not this row's. The row itself only records
/// membership.
/// </para>
/// </remarks>
public sealed class ReleaseVersion : BaseAuditableEntity
{
    private ReleaseVersion() { }

    internal ReleaseVersion(Guid releaseId, Guid versionId)
    {
        ReleaseId = releaseId;
        VersionId = versionId;
    }

    /// <summary>The release announcing this version.</summary>
    public Guid ReleaseId { get; private init; }

    /// <summary>The version announced.</summary>
    public Guid VersionId { get; private init; }

    /// <summary>The version announced, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public Version? Version { get; private init; }
}
