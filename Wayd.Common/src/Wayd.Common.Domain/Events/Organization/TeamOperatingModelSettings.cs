using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// How a team works under one operating model, as the team operating model events record it.
/// </summary>
/// <param name="TimeZone">The IANA id of the time zone the team's days are counted in.</param>
/// <param name="CommitmentGraceDays">How many days after a sprint's planned start its commitment is taken.</param>
public sealed record TeamOperatingModelSettings(Methodology Methodology, SizingMethod SizingMethod, string TimeZone, int CommitmentGraceDays);
