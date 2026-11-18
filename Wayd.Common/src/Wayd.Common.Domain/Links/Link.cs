using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Links;

namespace Wayd.Common.Domain.Links;

/// <summary>
/// A named web address attached to one record of any module: a project's charter, a team's wiki.
/// </summary>
/// <remarks>
/// The record is held by a registered owner type and an id, and never interpreted here. Who may read or change a
/// record's links is the owner type's answer, asked before any change reaches this class.
/// </remarks>
public sealed class Link : BaseEntity
{
    /// <summary>The longest name a link may have.</summary>
    public const int NameMaxLength = 128;

    private Link() { }

    private Link(string ownerType, Guid ownerId, string name, string url)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        Name = name;
        Url = url;
    }

    /// <summary>The registered owner type of the record the link is on, such as <c>ppm.project</c>.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The record the link is on.</summary>
    public Guid OwnerId { get; private init; }

    /// <summary>What the link is called.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>The address the link opens.</summary>
    public string Url
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Url)).Trim();
    } = default!;

    /// <summary>The link's name and address, as its events carry them.</summary>
    public LinkDetails Details => new(Name, Url);

    /// <summary>
    /// Adds a link to a record. Fails when the owner type is not registered or the address is not a web address.
    /// </summary>
    public static Result<Link> Create(string ownerType, Guid ownerId, string name, string url, EventActor actor, Instant timestamp)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        if (descriptor.IsFailure)
            return Result.Failure<Link>(descriptor.Error);

        Guard.Against.Default(ownerId, nameof(ownerId));
        Guard.Against.Null(actor, nameof(actor));

        if (!IsWebAddress(url))
            return Result.Failure<Link>(LinkErrors.UrlNotWebAddress);

        // The registered key rather than the caller's spelling, so a link is stored under one casing.
        var link = new Link(descriptor.Value.Key, ownerId, name, url);
        link.AddDomainEvent(new LinkAddedEvent(link.Id, link.OwnerType, link.OwnerId, link.Name, link.Url, actor, timestamp));

        return link;
    }

    /// <summary>
    /// Changes the link's name and address. Fails when a new address is not a web address; saving what the link
    /// already says changes nothing.
    /// </summary>
    public Result UpdateDetails(string name, string url, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));

        // Only a new address is checked: a link saved before addresses were checked can still be renamed.
        if (!string.Equals(url?.Trim(), Url, StringComparison.Ordinal) && !IsWebAddress(url))
            return Result.Failure(LinkErrors.UrlNotWebAddress);

        var previous = Details;

        Name = name;
        Url = url!;

        if (Details != previous)
            AddDomainEvent(new LinkDetailsUpdatedEvent(Id, OwnerType, OwnerId, Details, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Records that the link is being removed from its record. The caller removes the row.</summary>
    public void Remove(EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));

        AddDomainEvent(new LinkRemovedEvent(Id, OwnerType, OwnerId, Name, Url, actor, timestamp));
    }

    /// <summary>Whether the text is an absolute <c>http</c> or <c>https</c> address, the only kind a link may open.</summary>
    public static bool IsWebAddress(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
