using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Overwrites an intake request's editable fields. Each change is recorded as its own event, and fields that
/// did not change record nothing.
/// </summary>
/// <remarks>
/// Allowed for the requester and the desk's agents while the request is proposed, and for the desk's agents
/// while it is active. The caller must also be able to read the request's substance, because the command
/// carries the description: nobody overwrites what they cannot see.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Title">What is being asked for, in a line.</param>
/// <param name="Description">The ask in full.</param>
/// <param name="ExpectedBenefits">What gets better, and for whom, if the ask is delivered.</param>
/// <param name="ChannelId">
/// Where the request came from: a channel the request's desk offers, or the one it already has. Null keeps no
/// channel, and clears one only while the desk offers none.
/// </param>
/// <param name="NeedBy">When the request has to land and why, or null to clear it.</param>
public sealed record UpdateIntakeRequestCommand(
    Guid Id,
    string Title,
    string Description,
    string? ExpectedBenefits,
    Guid? ChannelId,
    IntakeRequestNeedByInput? NeedBy) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateIntakeRequestCommandValidator : CustomValidator<UpdateIntakeRequestCommand>
{
    public UpdateIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Description).NotEmpty();
        RuleFor(c => c.ChannelId).NotEqual(Guid.Empty);
        RuleFor(c => c.NeedBy!).SetValidator(new IntakeRequestNeedByInputValidator()).When(c => c.NeedBy is not null);
    }
}

public sealed class UpdateIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!await _dbContext.CanSeeSubstance(reader, request.Id, cancellationToken))
            return Result.Failure("Only someone who can read the request's detail can edit it.");

        var editor = new IntakeRequestEditor(reader.EmployeeId, reader.IsAgentOn(intakeRequest.DeskId),
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId));
        if (!intakeRequest.CanEdit(editor))
            return Result.Failure("Only the requester or an agent on the request's desk may edit this request.");

        var desk = await _dbContext.IntakeDesks
            .Include(d => d.Channels)
            .SingleAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);
        var channels = await _dbContext.IntakeChannels.ToListAsync(cancellationToken);
        IntakeChannel? channel = null;
        if (request.ChannelId is { } channelId)
        {
            channel = channels.FirstOrDefault(c => c.Id == channelId);
            if (channel is null)
                return Result.Failure("Intake channel not found.");
        }

        IntakeRequestNeedBy? needBy = null;
        if (request.NeedBy is not null)
        {
            var needByResult = await request.NeedBy.ToNeedBy(_dbContext, intakeRequest.NeedBy?.CommittedById, cancellationToken);
            if (needByResult.IsFailure)
                return needByResult;
            needBy = needByResult.Value;
        }

        var now = _dateTimeProvider.Now;

        // The only change that can still be refused goes first, so a refused update leaves the tracked request
        // untouched.
        var channelChanged = intakeRequest.ChangeChannel(channel, desk, channels, editor, now);
        if (channelChanged.IsFailure)
            return channelChanged;

        var result = Result.Combine(
            intakeRequest.Retitle(request.Title, editor, now),
            intakeRequest.UpdateDetails(request.Description, request.ExpectedBenefits, editor, now),
            intakeRequest.ChangeNeedBy(needBy, editor, now));
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
