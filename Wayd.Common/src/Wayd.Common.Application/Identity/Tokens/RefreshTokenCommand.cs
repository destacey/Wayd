namespace Wayd.Common.Application.Identity.Tokens;

public sealed record RefreshTokenCommand(string Token, string RefreshToken);
