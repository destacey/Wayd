namespace Wayd.DemandManagement.Application.IntakeChannels;

/// <summary>
/// The failures channel commands share, compared by the endpoint to choose a status code.
/// </summary>
public static class IntakeChannelErrors
{
    /// <summary>No channel has the id given.</summary>
    public const string NotFoundError = "Intake channel not found.";

    /// <summary>Another channel, active or archived, already has the name.</summary>
    public const string DuplicateNameError = "A channel with this name already exists.";
}
