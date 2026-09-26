namespace Wayd.Common.Application.SystemSettings.Scheduling.Dtos;

public sealed record SchedulingSettingsDto
{
    public required string DefaultTimeZone { get; init; }
    public int DefaultCommitmentGraceDays { get; init; }
}
