namespace Wayd.Common.Application.Identity.OidcProviders.Dtos;


/// <summary>
/// Compact list row. Same fields as the detail DTO; kept as a separate type so
/// the API contract is explicit and the listing endpoint stays stable if the
/// detail shape grows fields later.
/// </summary>
public sealed record OidcProviderListItemDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string ProviderType { get; set; }
    public required string Authority { get; set; }
    public bool IsEnabled { get; set; }
}
