namespace Wayd.Web.Api.Models.Links;

/// <summary>
/// A link as it is added to a record or changed.
/// </summary>
public sealed record LinkRequest
{
    /// <summary>What the link is called, up to 128 characters.</summary>
    public required string Name { get; set; }

    /// <summary>The full web address the link opens, starting with `http://` or `https://`.</summary>
    public required string Url { get; set; }
}
