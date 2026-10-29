namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

/// <summary>
/// Shared validation for Teams connection tests that require a delegated Entra access token.
/// </summary>
internal static class TeamsConnectionTestTokens
{
    internal const string MissingUserAssertionMessage =
        "Microsoft Entra sign-in is required to verify or test a Teams connection. "
        + "Wayd could not obtain an Entra access token from your browser session — sign in with "
        + "Microsoft Entra ID and try again.";

    internal static Result RequireUserAssertion(string? userAssertion)
    {
        if (string.IsNullOrWhiteSpace(userAssertion))
            return Result.Failure(MissingUserAssertionMessage);

        return Result.Success();
    }
}
