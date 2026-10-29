using Wayd.Common.Domain.Tagging;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One label on one intake request tag axis — "Payments" on Area, "Enterprise" on Customer segment.
/// </summary>
/// <remarks>
/// Changed only through <see cref="IntakeRequestTagCategory"/>.
/// </remarks>
public sealed class IntakeRequestTag : TagBase
{
    private IntakeRequestTag() { }

    internal IntakeRequestTag(Guid categoryId, string name, string? description)
        : base(categoryId, name, description)
    {
    }
}
