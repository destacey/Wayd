namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Knobs for the generated Request Desk dataset. The desks come from the organization — one per value stream,
/// plus IT and Security and Compliance where the company has people to staff them — so these shape only how
/// much demand reaches them.
/// </summary>
public sealed class DemandManagementOptions
{
    /// <summary>Average number of intake requests the company submits per month across its history.</summary>
    public int RequestsPerMonth { get; init; } = 12;
}
