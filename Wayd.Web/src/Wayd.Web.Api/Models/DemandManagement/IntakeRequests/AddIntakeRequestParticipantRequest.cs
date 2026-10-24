namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// Someone to give access to an intake request as a participant.
/// </summary>
public sealed record AddIntakeRequestParticipantRequest
{
    /// <summary>
    /// The employee to give access. They will read the request and may join its discussion, but cannot change it.
    /// </summary>
    public Guid EmployeeId { get; set; }
}

public sealed class AddIntakeRequestParticipantRequestValidator : CustomValidator<AddIntakeRequestParticipantRequest>
{
    public AddIntakeRequestParticipantRequestValidator()
    {
        RuleFor(r => r.EmployeeId).NotEmpty();
    }
}
