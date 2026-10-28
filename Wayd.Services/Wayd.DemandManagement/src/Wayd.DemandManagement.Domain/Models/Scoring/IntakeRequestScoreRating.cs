using Ardalis.GuardClauses;

namespace Wayd.DemandManagement.Domain.Models.Scoring;

/// <summary>
/// One criterion's rating within an <see cref="IntakeRequestScore"/>, with the criterion's name and token
/// copied at scoring time so the history reads correctly after the model changes hands or is archived.
/// </summary>
public sealed class IntakeRequestScoreRating : BaseAuditableEntity
{
    private IntakeRequestScoreRating() { }

    internal IntakeRequestScoreRating(
        Guid scoreId,
        Guid criterionId,
        string criterionName,
        string criterionToken,
        decimal ratingValue,
        Guid? ratingLevelId,
        string? ratingLevelLabel,
        int order)
    {
        ScoreId = scoreId;
        CriterionId = criterionId;
        CriterionName = criterionName;
        CriterionToken = criterionToken;
        RatingValue = ratingValue;
        RatingLevelId = ratingLevelId;
        RatingLevelLabel = ratingLevelLabel;
        Order = order;
    }

    /// <summary>
    /// The score this rating belongs to.
    /// </summary>
    public Guid ScoreId { get; private init; }

    /// <summary>
    /// The criterion that was rated.
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
    /// The number the calculation used: the chosen level's value, or a number entered directly.
    /// </summary>
    public decimal RatingValue { get; private init; }

    /// <summary>
    /// The chosen level, when the criterion is rated against a scale.
    /// </summary>
    public Guid? RatingLevelId { get; private init; }

    /// <summary>
    /// The chosen level's label, frozen at scoring time. Null for a number entered directly.
    /// </summary>
    public string? RatingLevelLabel { get; private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    /// <summary>
    /// The criterion's display order, copied from the model at scoring time.
    /// </summary>
    public int Order { get; private init; }
}
