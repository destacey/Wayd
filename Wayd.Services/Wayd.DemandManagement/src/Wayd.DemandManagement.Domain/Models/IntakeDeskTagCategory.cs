namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A company tag axis a desk chose to offer its agents, while the desk offers only the axes it chose.
/// </summary>
/// <remarks>
/// Kept when the axis is deactivated, so the desk offers it again if it is activated; an inactive axis is
/// offered by no desk in the meantime.
/// </remarks>
public sealed class IntakeDeskTagCategory
{
    private IntakeDeskTagCategory() { }

    internal IntakeDeskTagCategory(Guid deskId, Guid categoryId)
    {
        DeskId = deskId;
        CategoryId = categoryId;
    }

    /// <summary>The desk offering the axis.</summary>
    public Guid DeskId { get; private init; }

    /// <summary>The company tag axis the desk offers.</summary>
    public Guid CategoryId { get; private init; }
}
