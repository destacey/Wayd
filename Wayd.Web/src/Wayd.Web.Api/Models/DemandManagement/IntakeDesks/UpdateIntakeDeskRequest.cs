using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// An intake desk's settings and members, overwriting what it has. Settings that do not change record nothing.
/// </summary>
public sealed record UpdateIntakeDeskRequest
{
    /// <summary>
    /// The desk being changed; must match the route.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// What its members and administrators call the desk. Unique.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The requester-facing description of what kind of ask to bring here, which requesters choose by.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// How much of a request on the desk reaches people outside it: Open, SummaryOnly or Private. Only the
    /// Demand Management Admin may change it.
    /// </summary>
    public IntakeDeskVisibility Visibility { get; set; }

    /// <summary>
    /// Whether a requester can pick the desk. One they cannot is a destination agents route to.
    /// </summary>
    public bool IsRequesterSelectable { get; set; }

    /// <summary>
    /// Whether the desk accepts new requests.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// On a private desk, whether the requester may add participants themselves. Agents always can.
    /// </summary>
    public bool RequesterMayAddParticipants { get; set; }

    /// <summary>
    /// Everyone on the desk after the change, each with their role. Naming, removing or re-roling a Desk Admin
    /// needs the Demand Management Admin.
    /// </summary>
    public List<IntakeDeskMemberRequest> Members { get; set; } = [];

    /// <summary>
    /// The desk's <c>revision</c> as it was read before making this change. A desk changed since refuses the
    /// update with 409, so a stale page never undoes what someone else changed.
    /// </summary>
    public int ExpectedRevision { get; set; }

    public UpdateIntakeDeskCommand ToUpdateIntakeDeskCommand() =>
        new(Id, Name, Description, Visibility, IsRequesterSelectable, IsOpen, RequesterMayAddParticipants,
            [.. Members.Select(m => m.ToMemberDetails())], ExpectedRevision);
}

public sealed class UpdateIntakeDeskRequestValidator : CustomValidator<UpdateIntakeDeskRequest>
{
    public UpdateIntakeDeskRequestValidator()
    {
        RuleFor(d => d.Id).NotEmpty();
        RuleFor(d => d.Name).NotEmpty().MaximumLength(128);
        RuleFor(d => d.Description).NotEmpty().MaximumLength(1024);
        RuleFor(d => d.Visibility).IsInEnum();
        RuleFor(d => d.Members).NotNull()
            .Must(members => members.Select(m => m.EmployeeId).Distinct().Count() == members.Count)
            .WithMessage("An employee can hold only one role on a desk.");
        RuleForEach(d => d.Members).SetValidator(new IntakeDeskMemberRequestValidator());
    }
}
