namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// A requester withdrawing their own intake request.
/// </summary>
public sealed record WithdrawIntakeRequestRequest
{
    /// <summary>Why, if the requester wants to say; the desk reads it.</summary>
    public string? Note { get; set; }
}

public sealed class WithdrawIntakeRequestRequestValidator : CustomValidator<WithdrawIntakeRequestRequest>
{
    public WithdrawIntakeRequestRequestValidator()
    {
        RuleFor(r => r.Note).MaximumLength(1024);
    }
}
