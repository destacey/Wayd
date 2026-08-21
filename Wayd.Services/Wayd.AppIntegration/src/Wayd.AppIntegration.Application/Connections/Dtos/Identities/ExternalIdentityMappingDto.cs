using Mapster;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Identities;

/// <summary>
/// One external system user and the employee it resolves to, for the connection's People tab.
/// </summary>
public sealed record ExternalIdentityMappingDto : IMapFrom<ExternalIdentityMapping>
{
    public Guid Id { get; set; }

    /// <summary>The external system's stable identifier for this person.</summary>
    public required string ExternalId { get; set; }

    /// <summary>The address the external system reports, when it reports one.</summary>
    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>The account handle. Shown when there is no email.</summary>
    public string? Handle { get; set; }

    public Guid? EmployeeId { get; set; }

    /// <summary>The mapped employee's name, for display without a second lookup.</summary>
    public string? EmployeeName { get; set; }

    /// <summary>Unmapped, AutoMatched, ManuallyMapped, or Ignored.</summary>
    public required string Status { get; set; }

    /// <summary>When a sync last saw this identity on a work item.</summary>
    public Instant LastSeen { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ExternalIdentityMapping, ExternalIdentityMappingDto>()
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.EmployeeName, src => src.Employee == null
                ? null
                : src.Employee.Name.FirstName + " " + src.Employee.Name.LastName);
    }
}
