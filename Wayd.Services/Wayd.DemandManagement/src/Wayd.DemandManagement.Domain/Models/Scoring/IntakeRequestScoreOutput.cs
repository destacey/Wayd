using Ardalis.GuardClauses;

namespace Wayd.DemandManagement.Domain.Models.Scoring;

/// <summary>
/// One computed output within an <see cref="IntakeRequestScore"/>: the primary score or an intermediate value
/// such as Cost of Delay, kept so the score can be shown without re-evaluating the model.
/// </summary>
public sealed class IntakeRequestScoreOutput : BaseAuditableEntity
{
    private IntakeRequestScoreOutput() { }

    internal IntakeRequestScoreOutput(Guid scoreId, string token, string name, decimal value, bool isPrimary, int order)
    {
        ScoreId = scoreId;
        Token = token;
        Name = name;
        Value = value;
        IsPrimary = isPrimary;
        Order = order;
    }

    /// <summary>
    /// The score this output belongs to.
    /// </summary>
    public Guid ScoreId { get; private init; }

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
    /// The computed value.
    /// </summary>
    public decimal Value { get; private init; }

    /// <summary>
    /// Whether this was the model's primary output at scoring time.
    /// </summary>
    public bool IsPrimary { get; private init; }

    /// <summary>
    /// The output's evaluation order, copied from the model at scoring time.
    /// </summary>
    public int Order { get; private init; }
}
