namespace Wayd.Infrastructure.Auth.OAuth;

/// <summary>
/// An OAuth client of Wayd's authorization server, as its Client ID Metadata Document describes it. Every value
/// except <see cref="ClientId"/> is the client's own claim about itself, so show it to a person as such.
/// </summary>
/// <param name="ClientId">The URL of the client's metadata document, which is also its client identifier.</param>
/// <param name="Name">The client's name for itself, or the host of <paramref name="ClientId"/> when it gives none.</param>
/// <param name="RedirectUris">The only URIs an authorization response may be sent to; a request must name one exactly.</param>
/// <param name="ClientUri">The client's home page, when it names an HTTPS one.</param>
/// <param name="LogoUri">The client's logo, when it names an HTTPS one.</param>
public sealed record OAuthClient(
    string ClientId,
    string Name,
    IReadOnlyList<string> RedirectUris,
    string? ClientUri,
    string? LogoUri);
