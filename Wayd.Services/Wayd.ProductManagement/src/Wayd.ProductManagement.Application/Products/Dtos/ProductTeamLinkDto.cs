using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A team or team of teams linked to a product.
/// </summary>
/// <remarks>
/// Carries the product whichever side it is read from, so a rolled-up row on a parent product can say which
/// product beneath it the team is linked to, and a team's list can say where each product sits.
/// </remarks>
public sealed record ProductTeamLinkDto
{
    /// <summary>The product the team is linked to.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>Everything <c>product</c> sits inside, from its root down to its parent.</summary>
    public IReadOnlyList<NavigationDto> ProductPath { get; init; } = [];

    /// <summary>The team or team of teams linked.</summary>
    public ProductTeamNavigationDto Team { get; init; } = default!;
}

/// <summary>
/// A team or team of teams as Product Management knows it.
/// </summary>
public sealed record ProductTeamNavigationDto : NavigationDto
{
    /// <summary>The team's short code, unique across teams and teams of teams.</summary>
    public required string Code { get; init; }

    /// <summary>Whether this is a team or a team of teams, which decides where its page is.</summary>
    public TeamType Type { get; init; }

    /// <summary>Whether the team is active. An inactive team keeps its links but cannot take new ones.</summary>
    public bool IsActive { get; init; }
}
