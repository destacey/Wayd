using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.ProductManagement;

/// <summary>
/// How a product reaches one it depends on: waiting for an answer, or sending a message and carrying on.
/// </summary>
/// <remarks>
/// The multiplier on <see cref="DependencyStrength"/>. Strength says whether a product breaks without the one
/// it depends on; this says whether it breaks at the same moment. A hard synchronous dependency caps the
/// consumer's availability at the provider's, and a release of either has to be coordinated. A hard
/// asynchronous one turns the provider's downtime into a backlog the consumer works through afterwards.
/// <para>
/// A set rather than a choice: a pair commonly does both, calling for what it needs now and subscribing for
/// what it needs eventually. Flags are how that set is stored. Every boundary outside the domain — DTOs,
/// requests, event payloads, imports — carries the styles as a collection of names, so no consumer has to
/// know there is a bitmask, and asking "which of these are asynchronous" stays one containment test rather
/// than a list of members to keep in step.
/// </para>
/// <para>
/// No <c>None</c>. A zero member would be a second way to say "nothing" beside a null column, letting the
/// same fact be stored two ways. A dependency whose styles nobody has recorded holds null.
/// </para>
/// </remarks>
[Flags]
public enum InteractionStyle
{
    [Display(Name = "Synchronous", Description = "The product waits for an answer, so the one it depends on has to be up at that moment.", Order = 1)]
    Synchronous = 1,

    [Display(Name = "Asynchronous", Description = "The product sends or receives messages and carries on, so an outage becomes delay rather than failure.", Order = 2)]
    Asynchronous = 2
}
