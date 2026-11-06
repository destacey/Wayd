namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A company tag category a desk chose to offer its agents, while the desk offers only the categories it chose.
/// </summary>
/// <remarks>
/// Kept when the category is deactivated, so the desk offers it again if it is activated; an inactive category
/// is offered by no desk in the meantime.
/// </remarks>
public sealed class IntakeDeskTagCategory
{
    private IntakeDeskTagCategory() { }

    internal IntakeDeskTagCategory(Guid deskId, Guid categoryId)
    {
        DeskId = deskId;
        CategoryId = categoryId;
    }

    /// <summary>The desk offering the category.</summary>
    public Guid DeskId { get; private init; }

    /// <summary>The company tag category the desk offers.</summary>
    public Guid CategoryId { get; private init; }
}
