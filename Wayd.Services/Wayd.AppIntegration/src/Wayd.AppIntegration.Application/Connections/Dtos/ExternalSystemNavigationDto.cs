namespace Wayd.AppIntegration.Application.Connections.Dtos;

/// <summary>
/// The outside system a connection reaches, as a connection shows it: an Azure DevOps organization, an Entra
/// tenant or a Workday tenant. Several connections may share one.
/// </summary>
public sealed record ExternalSystemNavigationDto
{
    /// <summary>The unique identifier of the outside system.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The system's name, taken from the connection that reaches it: the Azure DevOps organization, the Entra
    /// tenant id, or the Workday tenant alias.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The address the system is reached at, when its connector has one.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Describes an outside system for display on a connection.</summary>
    public static ExternalSystemNavigationDto From(ExternalSystem system) => new()
    {
        Id = system.Id,
        Name = system.Name,
        BaseUrl = system.BaseUrl,
    };
}
