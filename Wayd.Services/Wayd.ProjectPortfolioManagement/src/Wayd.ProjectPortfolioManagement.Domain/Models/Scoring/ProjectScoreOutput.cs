using Ardalis.GuardClauses;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;

/// <summary>
/// A frozen snapshot of a single computed output within a <see cref="ProjectScore"/>. Every output the
/// model produced (the primary score and any intermediate values such as Cost of Delay) is captured so
/// the score can be displayed and ranked without re-evaluating the model.
/// </summary>
public sealed class ProjectScoreOutput : BaseAuditableEntity
{
    private ProjectScoreOutput() { }

    internal ProjectScoreOutput(
        Guid projectScoreId,
        string token,
        string name,
        decimal value,
        bool isPrimary,
        int order)
    {
        ProjectScoreId = projectScoreId;
        Token = token;
        Name = name;
        Value = value;
        IsPrimary = isPrimary;
        Order = order;
    }

    /// <summary>
    /// The ID of the <see cref="ProjectScore"/> this output belongs to.
    /// </summary>
    public Guid ProjectScoreId { get; private init; }

    /// <summary>
    /// The output's formula token, frozen at scoring time.
    /// </summary>
    public string Token
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Token)).Trim();
    } = default!;

    /// <summary>
    /// The output's name, frozen at scoring time.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The computed value of this output.
    /// </summary>
    public decimal Value { get; private init; }

    /// <summary>
    /// Whether this output was the model's primary score at scoring time.
    /// </summary>
    public bool IsPrimary { get; private init; }

    /// <summary>
    /// The evaluation order of the output, copied from the model at scoring time.
    /// </summary>
    public int Order { get; private init; }
}
