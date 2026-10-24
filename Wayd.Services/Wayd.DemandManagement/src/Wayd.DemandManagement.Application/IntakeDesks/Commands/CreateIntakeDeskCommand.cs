using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks.Commands;

/// <summary>
/// Creates an intake desk with its initial reviewers. The Administer permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// No desk is seeded, so this is how a company sets up the review it will staff. Changing a desk after it is
/// created arrives with desk administration.
/// </remarks>
/// <param name="Name">What reviewers and administrators call the desk.</param>
/// <param name="Description">The requester-facing description of what kind of ask to bring here.</param>
/// <param name="Visibility">How much of a request on the desk reaches people outside it.</param>
/// <param name="IsRequesterSelectable">Whether a requester can pick the desk.</param>
/// <param name="IsOpen">Whether the desk accepts new requests.</param>
/// <param name="RequesterMayAddParticipants">On a private desk, whether the requester may add participants themselves.</param>
/// <param name="ReviewerIds">The employees named as the desk's reviewers.</param>
public sealed record CreateIntakeDeskCommand(
    string Name,
    string Description,
    IntakeDeskVisibility Visibility,
    bool IsRequesterSelectable,
    bool IsOpen,
    bool RequesterMayAddParticipants,
    IReadOnlyList<Guid> ReviewerIds) : ICommand<ObjectIdAndKey>;

public sealed class CreateIntakeDeskCommandValidator : CustomValidator<CreateIntakeDeskCommand>
{
    public CreateIntakeDeskCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(1024);
        RuleFor(c => c.Visibility).IsInEnum();
        RuleFor(c => c.ReviewerIds).NotNull();
        RuleForEach(c => c.ReviewerIds).NotEmpty();
    }
}

public sealed class CreateIntakeDeskCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateIntakeDeskCommand, ObjectIdAndKey>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateIntakeDeskCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _dbContext.IntakeDesks.AnyAsync(d => d.Name == name, cancellationToken))
            return Result.Failure<ObjectIdAndKey>("A desk with this name already exists.");

        var reviewerIds = request.ReviewerIds.Distinct().ToArray();
        var activeReviewers = await _dbContext.Employees
            .CountAsync(e => reviewerIds.Contains(e.Id) && e.IsActive, cancellationToken);
        if (activeReviewers != reviewerIds.Length)
            return Result.Failure<ObjectIdAndKey>("Every reviewer must be an active employee.");

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var desk = IntakeDesk.Create(
            name,
            request.Description,
            request.Visibility,
            request.IsRequesterSelectable,
            request.IsOpen,
            request.RequesterMayAddParticipants,
            reviewerIds,
            EventActor.User(_currentUser.GetUserId(), employeeId),
            _dateTimeProvider.Now);

        _dbContext.IntakeDesks.Add(desk);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ObjectIdAndKey(desk.Id, desk.Key);
    }
}
