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

    /// <summary>The email address the external system reports for this person; null when it reports none.</summary>
    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>The person's account handle in the external system; identifies them when there is no email.</summary>
    public string? Handle { get; set; }

    public Guid? EmployeeId { get; set; }

    /// <summary>The mapped employee's first and last name; null when the identity is not mapped to an employee.</summary>
    public string? EmployeeName { get; set; }

    /// <summary>The mapping status: Unmapped, AutoMatched, ManuallyMapped, or Ignored.</summary>
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
