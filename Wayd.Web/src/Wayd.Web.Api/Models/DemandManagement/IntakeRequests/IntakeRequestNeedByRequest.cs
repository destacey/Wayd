using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// When a request has to land, and why. The date never travels without its basis and a reference.
/// </summary>
public sealed record IntakeRequestNeedByRequest
{
    /// <summary>
    /// The date the request is needed by.
    /// </summary>
    public LocalDate Date { get; set; }

    /// <summary>
    /// What makes it that date: a regulation, a contract, an external event, a commitment someone here made, or the requester's preference.
    /// </summary>
    public NeedByBasis Basis { get; set; }

    /// <summary>
    /// What the date rests on: the regulation and clause, the contract and clause, the event, or a note.
    /// </summary>
    public string Reference { get; set; } = default!;

    /// <summary>
    /// For a commitment, the employee who made it. Required for a commitment and refused for any other basis.
    /// </summary>
    public Guid? CommittedById { get; set; }

    /// <summary>
    /// For a commitment, the date it was made. Required for a commitment and refused for any other basis.
    /// </summary>
    public LocalDate? CommittedOn { get; set; }

    public IntakeRequestNeedByInput ToInput() => new(Date, Basis, Reference, CommittedById, CommittedOn);
}

public sealed class IntakeRequestNeedByRequestValidator : CustomValidator<IntakeRequestNeedByRequest>
{
    public IntakeRequestNeedByRequestValidator()
    {
        RuleFor(n => n.Basis).IsInEnum();
        RuleFor(n => n.Reference).NotEmpty().MaximumLength(1024);
        RuleFor(n => n.CommittedById).NotEqual(Guid.Empty);
    }
}
