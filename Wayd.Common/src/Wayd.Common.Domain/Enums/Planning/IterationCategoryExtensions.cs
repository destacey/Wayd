namespace Wayd.Common.Domain.Enums.Planning;

public static class IterationCategoryExtensions
{
    /// <summary>
    /// The type of a sprint mapped to a planning interval iteration of this category, unless the team set one.
    /// This switch is the single declaration per category, so a new category must choose its sprint type here.
    /// </summary>
    public static SprintType ToSprintType(this IterationCategory category) => category switch
    {
        IterationCategory.Development => SprintType.Standard,
        IterationCategory.InnovationAndPlanning => SprintType.NonStandard,
        _ => throw new InvalidOperationException($"No sprint type is declared for iteration category '{category}'.")
    };
}
