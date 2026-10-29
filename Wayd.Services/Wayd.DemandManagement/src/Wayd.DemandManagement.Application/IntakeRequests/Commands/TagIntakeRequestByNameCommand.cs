using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.IntakeRequestTags;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Files an intake request under a name an agent typed: the tag on an axis the request's desk offers when exactly
/// one has the name, otherwise the free tag with that name, created when none has it yet. Returns the id of the
/// tag applied.
/// </summary>
/// <remarks>
/// Needs an agent on the request's desk. Preferring a tag on an axis keeps a curated tag typed by hand from
/// becoming a free tag beside it. A free tag needs a desk that allows them; see <see cref="IntakeDesk.AllowsFreeTags"/>.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Name">The typed name.</param>
public sealed record TagIntakeRequestByNameCommand(Guid Id, string Name) : ICommand<Guid>;

public sealed class TagIntakeRequestByNameCommandValidator : CustomValidator<TagIntakeRequestByNameCommand>
{
    public TagIntakeRequestByNameCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(TagBase.NameMaxLength);
    }
}

public sealed class TagIntakeRequestByNameCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<TagIntakeRequestByNameCommand, Guid>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(TagIntakeRequestByNameCommand request, CancellationToken cancellationToken)
    {
        var name = TagBase.ValidateFreeName(request.Name);
        if (name.IsFailure)
            return Result.Failure<Guid>(name.Error);

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken, includeTags: true);
        if (intakeRequest is null)
            return Result.Failure<Guid>(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure<Guid>(IntakeRequestAuthorization.NotAnAgentError);

        var desk = await _dbContext.IntakeDesks
            .Include(d => d.TagCategories)
            .FirstAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);

        var lowered = name.Value.ToLower();
        var categories = await _dbContext.IntakeRequestTagCategories
            .Include(c => c.Tags)
            .Where(c => c.Tags.Any(t => t.IsActive && t.Name.ToLower() == lowered))
            .ToListAsync(cancellationToken);
        var onAxes = categories
            .Where(desk.Offers)
            .Select(c => (Category: c, Tag: c.Tags.First(t => t.IsActive && string.Equals(t.Name, name.Value, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (onAxes.Count > 1)
            return Result.Failure<Guid>(IntakeRequestTagErrors.AmbiguousName);

        var actor = EventActor.User(_currentUser.GetUserId(), reader.EmployeeId);
        var now = _dateTimeProvider.Now;

        IntakeRequestTag tag;
        IntakeRequestTagCategory? category = null;
        if (onAxes.Count == 1)
        {
            (category, tag) = onAxes[0];
        }
        else
        {
            if (!desk.AllowsFreeTags)
                return Result.Failure<Guid>(IntakeRequest.FreeTagsNotAllowedError);

            var found = await FreeIntakeRequestTags.FindOrCreate(_dbContext, name.Value, actor, now, cancellationToken);
            if (found.IsFailure)
                return Result.Failure<Guid>(found.Error);

            tag = found.Value;
        }

        var result = intakeRequest.Tag(tag, category, desk, actor, now);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The request was given the tag, or the free tag was deleted for being unused, since the lookups.
            return Result.Failure<Guid>(IntakeRequestTagErrors.ChangedWhileSaving);
        }

        return tag.Id;
    }
}
