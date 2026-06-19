using Wayd.Common.Domain.Enums;

namespace Wayd.AppIntegration.Domain.Models.AICapabilities.ObjectiveHealthCheckSummary;

/// <summary>
/// Used as a type to generate a schema for Structured Outputs from LLMs around the AI Assisted Objective Health Checks feature.
/// </summary>
public sealed class ObjectiveHealthCheckSummaryResponse
{
    public required HealthStatus HealthStatus { get; set; }

    public string? Description { get; set; }
}