using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Imports.Dtos;

/// <summary>
/// One kind of import the caller may submit.
/// </summary>
/// <remarks>
/// The limits are here because they are the definition's own, not global: a page that offers a file picker
/// should be able to say what it will accept before the person uploads fifty thousand rows and is told no.
/// </remarks>
public sealed record ImportDefinitionDto(
    string Key,
    string DisplayName,
    ImportAtomicity Atomicity,
    int MaxRows,
    int InlineThreshold);
