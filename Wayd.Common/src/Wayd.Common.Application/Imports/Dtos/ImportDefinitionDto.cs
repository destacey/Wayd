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
/// </remarks>
public sealed record ImportDefinitionDto(
    string Key,
    string DisplayName,
    ImportAtomicity Atomicity,
    int MaxRows,
    bool CanSubmit);
