using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.Planning.Commands;

/// <summary>
/// A record a poker round will estimate, as the module that owns it describes it to Planning.
/// </summary>
/// <param name="Kind">The kind of record.</param>
/// <param name="Id">The record's id in the module that owns it.</param>
/// <param name="DisplayKey">The key people know the record by.</param>
/// <param name="Title">The record's title, which voters read.</param>
/// <param name="Description">The record's description, which voters read, or null when it has none.</param>
public sealed record PokerRoundSubjectInput(PokerRoundSubjectKind Kind, Guid Id, string DisplayKey, string Title, string? Description);
