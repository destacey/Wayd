using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// An intake request's need-by date, its basis and what the date rests on.
/// </summary>
public sealed record IntakeRequestNeedByDto
{
    /// <summary>The date the request is needed by.</summary>
    public LocalDate Date { get; init; }

    /// <summary>What makes it that date.</summary>
    public NeedByBasis Basis { get; init; }

    /// <summary>Whether something outside the company set the date or someone here chose it. Derived from the basis.</summary>
    public NeedByBasisFamily Family => Basis.Family();

    /// <summary>What the date rests on: the regulation and clause, the contract and clause, the event, or a note.</summary>
    public required string Reference { get; init; }

    /// <summary>For a commitment, the employee who made it.</summary>
    public EmployeeNavigationDto? CommittedBy { get; init; }

    /// <summary>For a commitment, the date it was made.</summary>
    public LocalDate? CommittedOn { get; init; }
}
