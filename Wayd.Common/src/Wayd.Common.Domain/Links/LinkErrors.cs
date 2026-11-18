namespace Wayd.Common.Domain.Links;

/// <summary>
/// The failures links report, shared by the domain, the handlers and their tests.
/// </summary>
public static class LinkErrors
{
    /// <summary>No owner type was given.</summary>
    public const string OwnerTypeRequired = "A link owner type is required.";

    /// <summary>An owner type no module registered.</summary>
    public static string UnknownOwnerType(string key) => $"'{key}' is not a kind of record that can carry links.";

    /// <summary>A URL that is not an absolute <c>http</c> or <c>https</c> address.</summary>
    public const string UrlNotWebAddress = "A link's URL must be a full web address starting with http:// or https://.";
}
