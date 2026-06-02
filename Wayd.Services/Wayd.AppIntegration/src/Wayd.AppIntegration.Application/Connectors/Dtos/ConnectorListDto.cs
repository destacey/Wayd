using Wayd.Common.Application.Dtos;

namespace Wayd.AppIntegration.Application.Connectors.Dtos;

public sealed class ConnectorListDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The connector's category — drives grouping in the connector-picker UI and the
    /// single-active-per-category rule (e.g. PeopleSync).
    /// </summary>
    public required SimpleNavigationDto Category { get; set; }
}
