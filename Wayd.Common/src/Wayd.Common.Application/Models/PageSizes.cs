namespace Wayd.Common.Application.Models;

/// <summary>
/// The page sizes every paged list accepts, whether it pages by number (<see cref="PageRequest"/>) or by cursor
/// (<see cref="CursorRequest"/>).
/// </summary>
public static class PageSizes
{
    /// <summary>The page size a caller gets when they do not ask for one.</summary>
    public const int Default = 50;

    /// <summary>The largest page a caller may ask for.</summary>
    public const int Max = 500;
}
