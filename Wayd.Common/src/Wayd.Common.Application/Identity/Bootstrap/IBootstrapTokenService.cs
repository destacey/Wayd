namespace Wayd.Common.Application.Identity.Bootstrap;

/// <summary>
/// Manages the one-time bootstrap token used to create the first admin user.
/// The token is generated in-memory on startup when no users exist, logged to
/// the console, and consumed exactly once via POST /api/auth/setup.
/// </summary>
public interface IBootstrapTokenService
{
    /// <summary>Returns true if a bootstrap token is currently active.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Validates the supplied token without consuming it.
    /// Returns false if no token is active or the token does not match.
    /// </summary>
    bool Validate(string token);

    /// <summary>
    /// Invalidates the active token, preventing any further use.
    /// Should be called after setup completes successfully.
    /// </summary>
    void Consume();
}
