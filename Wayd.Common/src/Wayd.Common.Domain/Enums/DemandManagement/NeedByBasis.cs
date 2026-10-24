using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.DemandManagement;

/// <summary>
/// What makes a request's need-by date that date. A date never travels without one.
/// </summary>
/// <remarks>
/// Two requests can both say 31 December, one because a standard requires it and one because somebody
/// picked it in a meeting; the basis is what tells a reviewer which. Its <see cref="NeedByBasisFamily"/> is
/// derived through <see cref="NeedByBasisExtensions.Family"/>, never stored.
/// </remarks>
public enum NeedByBasis
{
    [Display(Name = "Regulatory", Description = "A regulation or standard sets the date.", Order = 1)]
    Regulatory = 1,

    [Display(Name = "Contractual", Description = "A contract clause sets the date.", Order = 2)]
    Contractual = 2,

    [Display(Name = "External Event", Description = "Something outside the company sets the date, such as an end of support or a customer go-live.", Order = 3)]
    ExternalEvent = 3,

    /// <summary>
    /// A commitment someone here made.
    /// </summary>
    /// <remarks>
    /// The only basis that also records who made it and when, because a commitment without an owner cannot
    /// be renegotiated, only missed.
    /// </remarks>
    [Display(Name = "Commitment", Description = "A commitment someone here made.", Order = 4)]
    Commitment = 4,

    [Display(Name = "Requester Preference", Description = "The date the requester would like.", Order = 5)]
    RequesterPreference = 5,
}

/// <summary>
/// Whether a need-by date is set from outside the company or chosen inside it.
/// </summary>
/// <remarks>
/// A fixed date is a fact that moves only if the world moves; a chosen date has an owner and can be
/// renegotiated rather than silently missed.
/// </remarks>
public enum NeedByBasisFamily
{
    [Display(Name = "Fixed for us", Description = "Something outside the company sets the date.", Order = 1)]
    FixedForUs = 1,

    [Display(Name = "We chose it", Description = "Someone here set the date.", Order = 2)]
    ChosenByUs = 2,
}

/// <summary>
/// Derives facts about a <see cref="NeedByBasis"/> that are part of what the basis means.
/// </summary>
public static class NeedByBasisExtensions
{
    /// <summary>
    /// The family the basis belongs to.
    /// </summary>
    public static NeedByBasisFamily Family(this NeedByBasis basis) => basis switch
    {
        NeedByBasis.Regulatory or NeedByBasis.Contractual or NeedByBasis.ExternalEvent => NeedByBasisFamily.FixedForUs,
        NeedByBasis.Commitment or NeedByBasis.RequesterPreference => NeedByBasisFamily.ChosenByUs,
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unknown need-by basis."),
    };

    /// <summary>
    /// Whether the basis records who made the commitment and when.
    /// </summary>
    public static bool RecordsCommitment(this NeedByBasis basis) => basis == NeedByBasis.Commitment;
}
