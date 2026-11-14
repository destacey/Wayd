using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Integrations;

/// <summary>
/// One thing in another system that a Wayd record points at.
/// Stored as JSON so any record can keep the same shape.
/// </summary>
public sealed class RecordIntegration
{
    /// <summary>The longest external id this can store.</summary>
    public const int ExternalIdMaxLength = 512;

    /// <summary>The longest title this can store.</summary>
    public const int TitleMaxLength = 256;

    /// <summary>The longest link this can store.</summary>
    public const int LinkMaxLength = 2048;

    private RecordIntegration() { }

    private RecordIntegration(
        Connector connector,
        string? externalId,
        string? title,
        string? link,
        bool createdByWayd)
    {
        Connector = connector;
        ExternalId = externalId;
        Title = title;
        Link = link;
        CreatedByWayd = createdByWayd;
    }

    /// <summary>Which system this points at.</summary>
    public Connector Connector { get; private set; }

    /// <summary>The id in that system, when Wayd knows one. A pasted link has none.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>The name shown for the linked thing. Null when the link stands alone.</summary>
    public string? Title { get; private set; }

    /// <summary>The link that opens it.</summary>
    public string? Link { get; private set; }

    /// <summary>True only when Wayd created it. A pasted link is not one Wayd created.</summary>
    public bool CreatedByWayd { get; private set; }

    /// <summary>A link to something in <paramref name="connector"/>.</summary>
    public static RecordIntegration Create(
        Connector connector,
        string? externalId,
        string? title,
        string? link,
        bool createdByWayd) =>
        new(connector, externalId, title, link, createdByWayd);
}
