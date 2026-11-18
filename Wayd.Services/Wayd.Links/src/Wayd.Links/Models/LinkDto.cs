using Mapster;
using Wayd.Common.Domain.Links;

namespace Wayd.Links.Models;

/// <summary>One link on a record.</summary>
public sealed record LinkDto : IMapFrom<Link>
{
    /// <summary>The link's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The registered owner type of the record the link is on, such as <c>ppm.project</c>.</summary>
    public required string OwnerType { get; set; }

    /// <summary>The id of the record the link is on.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>What the link is called.</summary>
    public required string Name { get; set; }

    /// <summary>The address the link opens.</summary>
    public required string Url { get; set; }
}

/// <summary>A record's links, and what the caller may do with them.</summary>
public sealed record RecordLinksDto
{
    /// <summary>Whether the caller may add, change and remove the record's links.</summary>
    public required bool CanManage { get; init; }

    /// <summary>The record's links, by name.</summary>
    public required IReadOnlyList<LinkDto> Links { get; init; }
}
