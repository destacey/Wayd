using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Files an intake request under a tag. Needs an agent on the request's desk.
/// </summary>
/// <remarks>
/// On an axis that takes one tag, this replaces the tag the request held there. The request's own people see
/// its tags but do not change them: filing is the reviewers' call.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="TagId">The tag to apply, which must be active on an active axis.</param>
public sealed record TagIntakeRequestCommand(Guid Id, Guid TagId) : ICommand, IRequireLinkedEmployee;

public sealed class TagIntakeRequestCommandValidator : CustomValidator<TagIntakeRequestCommand>
{
    public TagIntakeRequestCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.TagId).NotEmpty();
    }
}

public sealed class TagIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<TagIntakeRequestCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(TagIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken, includeTags: true);
        if (intakeRequest is null)
            return Result.Failure(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(IntakeRequestAuthorization.NotAnAgentError);

        var category = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Tags.Any(t => t.Id == request.TagId), cancellationToken);
        var tag = category?.Tags.First(t => t.Id == request.TagId);
        if (category is null || tag is null)
            return Result.Failure(IntakeRequestTagCategoryErrors.TagNotFoundError);

        var result = intakeRequest.Tag(tag, category,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
