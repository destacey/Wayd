using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// One employee to name on an intake desk, with their role.
/// </summary>
public sealed record IntakeDeskMemberRequest
{
    /// <summary>
    /// The employee to name on the desk.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// What they may do on the desk: Admin runs it, Agent acts on its requests.
    /// </summary>
    public IntakeDeskRole Role { get; set; }

    public IntakeDeskMemberDetails ToMemberDetails() => new(EmployeeId, Role);
}

public sealed class IntakeDeskMemberRequestValidator : CustomValidator<IntakeDeskMemberRequest>
{
    public IntakeDeskMemberRequestValidator()
    {
        RuleFor(m => m.EmployeeId).NotEmpty();
        RuleFor(m => m.Role).IsInEnum();
    }
}
