using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Submits a request to a desk, for the caller or on someone else's behalf.
/// </summary>
/// <param name="Title">What is being asked for, in a line. Everyone who can view requests may read it.</param>
/// <param name="Description">The ask in full.</param>
/// <param name="ExpectedBenefits">What gets better, and for whom, if the ask is delivered.</param>
/// <param name="DeskId">The desk to submit to: one that is open and that requesters can pick.</param>
/// <param name="ChannelId">Where the request came from: an active channel.</param>
/// <param name="RequesterId">The employee the request is for, when it is not the caller.</param>
/// <param name="NeedBy">When the request has to land, and why.</param>
public sealed record CreateIntakeRequestCommand(
    string Title,
    string Description,
    string? ExpectedBenefits,
    Guid DeskId,
    Guid ChannelId,
    Guid? RequesterId,
    IntakeRequestNeedByInput? NeedBy) : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

public sealed class CreateIntakeRequestCommandValidator : CustomValidator<CreateIntakeRequestCommand>
{
    public CreateIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).NotEmpty();
        RuleFor(c => c.DeskId).NotEmpty();
        RuleFor(c => c.ChannelId).NotEmpty();
        RuleFor(c => c.RequesterId).NotEqual(Guid.Empty);
        RuleFor(c => c.NeedBy!).SetValidator(new IntakeRequestNeedByInputValidator()).When(c => c.NeedBy is not null);
    }
}

public sealed class CreateIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IStatusResolver statusResolver)
    : ICommandHandler<CreateIntakeRequestCommand, ObjectIdAndKey>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var desk = await _dbContext.IntakeDesks.FirstOrDefaultAsync(d => d.Id == request.DeskId, cancellationToken);
        if (desk is null || !desk.IsRequesterSelectable)
            return Result.Failure<ObjectIdAndKey>("Choose a desk that requesters can submit to.");

        var channel = await _dbContext.IntakeChannels.FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);
        if (channel is null)
            return Result.Failure<ObjectIdAndKey>("Intake channel not found.");

        if (!await _dbContext.IsActiveEmployee(employeeId.Value, cancellationToken))
            return Result.Failure<ObjectIdAndKey>("Only an active employee can submit a request.");

        var requesterId = request.RequesterId ?? employeeId.Value;
        if (requesterId != employeeId.Value && !await _dbContext.IsActiveEmployee(requesterId, cancellationToken))
            return Result.Failure<ObjectIdAndKey>("The requester must be an active employee.");

        IntakeRequestNeedBy? needBy = null;
        if (request.NeedBy is not null)
        {
            var needByResult = await request.NeedBy.ToNeedBy(_dbContext, currentCommittedById: null, cancellationToken);
            if (needByResult.IsFailure)
                return Result.Failure<ObjectIdAndKey>(needByResult.Error);
            needBy = needByResult.Value;
        }

        // The desk is the assignment scope; a desk without a workflow of its own follows the organization's.
        var submitted = await _statusResolver.ForAlias(
            DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, (int)IntakeRequestStatusAlias.Submitted, cancellationToken);
        if (submitted.IsFailure)
            return Result.Failure<ObjectIdAndKey>(submitted.Error);

        var created = IntakeRequest.Create(
            request.Title,
            request.Description,
            request.ExpectedBenefits,
            requesterId,
            employeeId.Value,
            desk,
            channel,
            needBy,
            origin: null,
            submitted.Value,
            EventActor.User(_currentUser.GetUserId(), employeeId),
            _dateTimeProvider.Now);
        if (created.IsFailure)
            return Result.Failure<ObjectIdAndKey>(created.Error);

        _dbContext.IntakeRequests.Add(created.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ObjectIdAndKey(created.Value.Id, created.Value.Key);
    }
}
