using Ardalis.GuardClauses;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

/// <summary>
/// A frozen snapshot of a single criterion's rating within a <see cref="ProjectScore"/>. The criterion
/// name and token are copied at scoring time so the history stays accurate even if the underlying
/// scoring model is later archived or replaced.
/// </summary>
public sealed class ProjectScoreRating : BaseAuditableEntity
{
    private ProjectScoreRating() { }

    internal ProjectScoreRating(
        Guid projectScoreId,
        Guid criterionId,
        string criterionName,
        string criterionToken,
        decimal ratingValue,
        Guid? ratingLevelId,
        string? ratingLevelLabel,
        int order)
    {
        ProjectScoreId = projectScoreId;
        CriterionId = criterionId;
        CriterionName = criterionName;
        CriterionToken = criterionToken;
        RatingValue = ratingValue;
        RatingLevelId = ratingLevelId;
        RatingLevelLabel = ratingLevelLabel;
        Order = order;
    }

    /// <summary>
    /// The ID of the <see cref="ProjectScore"/> this rating belongs to.
    /// </summary>
    public Guid ProjectScoreId { get; private init; }

    /// <summary>
    /// The ID of the scoring model criterion that was rated.
    /// </summary>
    public Guid CriterionId { get; private init; }

    /// <summary>
    /// The criterion's name, frozen at scoring time.
    /// </summary>
    public string CriterionName
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(CriterionName)).Trim();
    } = default!;

    /// <summary>
    /// The criterion's formula token, frozen at scoring time.
    /// </summary>
    public string CriterionToken
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(CriterionToken)).Trim();
    } = default!;

    /// <summary>
    /// The numeric value used in the calculation for this criterion (either a scale level's value or a
    /// free numeric entry).
    /// </summary>
    public decimal RatingValue { get; private init; }

    /// <summary>
    /// The ID of the selected rating level, when the criterion was rated against a scale.
    /// </summary>
    public Guid? RatingLevelId { get; private init; }

    /// <summary>
    /// The selected rating level's label, frozen at scoring time. Null for free numeric entries.
    /// </summary>
    public string? RatingLevelLabel { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>
    /// The display order of the criterion, copied from the model at scoring time.
    /// </summary>
    public int Order { get; private init; }
}
