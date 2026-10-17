namespace Wayd.Common.Application.Identity.Tokens;

/// <summary>
/// Exchanges a token issued by an external OIDC provider for a Wayd JWT. The server
/// validates <c>subjectToken</c> against the named provider and returns a Wayd JWT
/// carrying the user's permission claims.
/// </summary>
/// <remarks>
/// The frontend obtains the subject token from an OIDC provider (e.g., an Entra access
/// token acquired via MSAL's <c>acquireTokenSilent</c> with the API scope) and hands it to
/// <c>POST /api/auth/exchange</c>.
///
/// Named after RFC 8693's <c>subject_token</c> — the token being exchanged for
/// a new one. For Entra today this is the access token for the API scope;
/// other providers may use a different token type.
/// </remarks>
/// <param name="Provider">Provider name — e.g., <c>"MicrosoftEntraId"</c>.</param>
/// <param name="SubjectToken">The token issued by the external provider, to be exchanged.</param>
public sealed record ExchangeTokenCommand(string Provider, string SubjectToken);
