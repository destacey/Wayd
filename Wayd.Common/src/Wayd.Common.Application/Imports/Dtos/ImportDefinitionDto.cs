using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Dtos;

/// <summary>
/// One kind of import the caller may see.
/// </summary>
/// <remarks>
/// The limits are here because they are the definition's own, not global: a page that offers a file picker
/// should be able to say what it will accept before the person uploads fifty thousand rows and is told no.
/// <para>
/// The list is what the caller may <em>see</em>, which oversight of every import type widens beyond what
/// they may submit — so <c>CanSubmit</c> is what an upload form must filter on, not the list itself.
/// </para>
/// <para>
/// <c>IsEnabled</c> is apart from <c>CanSubmit</c> because it can change during a session: a type whose area is
/// switched off is still listed, with its limits, so its past runs stay readable and a caller that switches
/// the area on next can plan for it.
/// </para>
/// </remarks>
public sealed record ImportDefinitionDto(
    string Key,
    string DisplayName,
    ImportAtomicity Atomicity,
    string? GroupNoun,
    int MaxRows,
    int PreflightMaxRows,
    bool CanSubmit,
    bool IsEnabled);
