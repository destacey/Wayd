namespace Wayd.Common.Application.Identity.OidcProviders.Dtos;


/// <summary>
/// An OIDC identity provider as it appears in the list of providers.
/// </summary>
/// <remarks>
/// Kept separate from the detail DTO so the API contract is explicit and the listing
/// endpoint stays stable if the detail shape grows fields later.
/// </remarks>
public sealed record OidcProviderListItemDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string ProviderType { get; set; }
    public required string Authority { get; set; }
    public bool IsEnabled { get; set; }
}
