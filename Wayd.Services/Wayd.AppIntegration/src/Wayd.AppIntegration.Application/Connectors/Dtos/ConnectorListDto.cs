namespace Wayd.AppIntegration.Application.Connectors.Dtos;

public sealed class ConnectorListDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The capabilities this connector supports, each with its display category. Multi-surface
    /// connectors can support more than one capability while still using one connection.
    /// </summary>
    public required IReadOnlyList<ConnectorCapabilityDto> Capabilities { get; set; }
}
