using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Overwrites a desk's settings and members. Each change is recorded as its own event, and settings that did
/// not change record nothing.
/// </summary>
/// <remarks>
/// A Desk Admin on the desk changes its details, whether it is open, its requester settings and its agents.
/// Its visibility, and who its Desk Admins are, only the Demand Management Admin changes: a Desk Admin who
/// could open their own desk could publish everything on it, and one who could name Desk Admins could raise
/// anyone's access. A member removed while owning requests on the desk is unassigned from them, because the
/// assignee of a request is always a member of its desk.
/// <para>
/// The desk is saved whole, so a change prepared against an older revision is refused rather than undoing
/// whatever changed since; two saves racing each other are caught the same way by the revision's
/// concurrency check.
/// </para>
/// </remarks>
/// <param name="Id">The desk.</param>
/// <param name="Name">What its members and administrators call the desk.</param>
/// <param name="Description">The requester-facing description of what kind of ask to bring here.</param>
/// <param name="Visibility">How much of a request on the desk reaches people outside it.</param>
/// <param name="IsRequesterSelectable">Whether a requester can pick the desk.</param>
/// <param name="IsOpen">Whether the desk accepts new requests.</param>
/// <param name="RequesterMayAddParticipants">On a private desk, whether the requester may add participants themselves.</param>
/// <param name="Members">Everyone on the desk after the change, each with their role.</param>
/// <param name="ExpectedRevision">The desk's <c>Revision</c> when the change was prepared; a desk changed since refuses it.</param>
public sealed record UpdateIntakeDeskCommand(
    Guid Id,
    string Name,
    string Description,
    IntakeDeskVisibility Visibility,
    bool IsRequesterSelectable,
    bool IsOpen,
    bool RequesterMayAddParticipants,
    IReadOnlyList<IntakeDeskMemberDetails> Members,
    int ExpectedRevision) : ICommand;

public sealed class UpdateIntakeDeskCommandValidator : CustomValidator<UpdateIntakeDeskCommand>
{
    public UpdateIntakeDeskCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(1024);
        RuleFor(c => c.Visibility).IsInEnum();
        RuleFor(c => c.Members).NotNull()
            .Must(members => members.Select(m => m.EmployeeId).Distinct().Count() == members.Count)
            .WithMessage("An employee can hold only one role on a desk.");
        RuleForEach(c => c.Members).ChildRules(member =>
        {
            member.RuleFor(m => m.EmployeeId).NotEmpty();
            member.RuleFor(m => m.Role).IsInEnum();
        });
    }
}

public sealed class UpdateIntakeDeskCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateIntakeDeskCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateIntakeDeskCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var desk = await _dbContext.IntakeDesks
            .Include(d => d.Members)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (desk is null)
            return Result.Failure(IntakeDeskAccess.NotFoundError);

        if (!reader.IsDeskAdminOn(desk.Id))
            return Result.Failure(IntakeDeskAccess.NotADeskAdminError);

        if (!desk.IsAtRevision(request.ExpectedRevision))
            return Result.Failure(IntakeDeskAccess.StaleError);

        if (request.Visibility != desk.Visibility && !reader.IsDemandManagementAdmin)
            return Result.Failure(IntakeDeskAccess.VisibilityNeedsAdministerError);

        var membershipChange = desk.MembershipChange(request.Members);
        if (membershipChange.TouchesDeskAdmins && !reader.IsDemandManagementAdmin)
            return Result.Failure(IntakeDeskAccess.DeskAdminsNeedAdministerError);

        var name = request.Name.Trim();
        if (await _dbContext.IntakeDesks.AnyAsync(d => d.Id != desk.Id && d.Name == name, cancellationToken))
            return Result.Failure("A desk with this name already exists.");

        // Members already on the desk may stay after their employee record goes inactive; only someone newly
        // named must be active.
        var addedIds = membershipChange.Added.Select(m => m.EmployeeId).ToArray();
        var activeAdded = await _dbContext.Employees.CountAsync(e => addedIds.Contains(e.Id) && e.IsActive, cancellationToken);
        if (activeAdded != addedIds.Length)
            return Result.Failure("Every member must be an active employee.");

        var actor = EventActor.User(_currentUser.GetUserId(), reader.EmployeeId);
        var now = _dateTimeProvider.Now;
        var result = Result.Combine(
            desk.UpdateDetails(request.Name, request.Description, actor, now),
            desk.ChangeVisibility(request.Visibility, actor, now),
            request.IsOpen ? desk.Open(actor, now) : desk.Close(actor, now),
            desk.ChangeRequesterSettings(request.IsRequesterSelectable, request.RequesterMayAddParticipants, actor, now),
            desk.ChangeMembers(request.Members, actor, now));
        if (result.IsFailure)
            return result;

        var removedIds = membershipChange.Removed.Select(m => (Guid?)m.EmployeeId).ToArray();
        if (removedIds.Length > 0)
        {
            var owned = await _dbContext.IntakeRequests
                .Where(r => r.DeskId == desk.Id && removedIds.Contains(r.AssigneeId))
                .ToListAsync(cancellationToken);
            foreach (var intakeRequest in owned)
                intakeRequest.ChangeAssignee(null, actor, now);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The desk's revision moved on, or a request was assigned to a member this change removes after
            // the requests to unassign were read; either way the desk changed underneath this save.
            return Result.Failure(IntakeDeskAccess.StaleError);
        }

        return Result.Success();
    }
}
