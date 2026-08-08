using Mapster;
using Wayd.Common.Domain.Employees;

namespace Wayd.Common.Application.Employees.Dtos;

/// <summary>
/// One work address on an employee. Only work addresses are recorded — home and personal addresses are
/// filtered out at the connector.
/// </summary>
public sealed record EmployeeEmailDto : IMapFrom<EmployeeEmail>
{
    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets the work email address.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// Indicates this is the employee's primary address — the one <see cref="EmployeeDetailsDto.Email"/>
    /// carries and that the rest of the app resolves against. Exactly one address per employee has this.
    /// </summary>
    public bool IsPrimary { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<EmployeeEmail, EmployeeEmailDto>()
            .Map(dest => dest.Email, src => src.Email.Value);
    }
}
