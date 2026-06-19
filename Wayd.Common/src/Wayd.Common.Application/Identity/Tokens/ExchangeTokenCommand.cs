namespace Wayd.Common.Application.Identity.Tokens;

/// <summary>
/// Exchanges an external identity-provider token for a Wayd JWT. The frontend
/// obtains <paramref name="SubjectToken"/> from an OIDC provider (e.g., an
/// Entra access token acquired via MSAL's <c>acquireTokenSilent</c> with the
/// API scope) and hands it to <c>POST /api/auth/exchange</c>; the server
/// validates it and mints a Wayd JWT carrying permission claims.
///
/// Named after RFC 8693's <c>subject_token</c> — the token being exchanged for
/// a new one. For Entra today this is the access token for the API scope;
/// other providers may use a different token type.
/// </summary>
/// <param name="Provider">Provider name — e.g., <c>"MicrosoftEntraId"</c>.</param>
/// <param name="SubjectToken">The token issued by the external provider, to be exchanged.</param>
public sealed record ExchangeTokenCommand(string Provider, string SubjectToken);
