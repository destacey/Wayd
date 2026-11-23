namespace Wayd.Planning.Application.EstimationScales.Commands;

/// <summary>
/// Set an estimation scale's name, description and values. Values that differ from the current version's
/// publish the next version; the same values publish nothing.
/// </summary>
public sealed record UpdateEstimationScaleCommand(Guid Id, string Name, string? Description, List<EstimationScaleValueInput> Values) : ICommand;

public sealed class UpdateEstimationScaleCommandValidator : CustomValidator<UpdateEstimationScaleCommand>
{
    public UpdateEstimationScaleCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.Values)
            .NotEmpty()
            .SetValidator(new EstimationScaleValuesValidator());
    }
}

public sealed class UpdateEstimationScaleCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider) : ICommandHandler<UpdateEstimationScaleCommand>
{
    /// <summary>The conflict when another edit published a version of the scale while this one was being saved.</summary>
    public const string VersionConflictError = "The estimation scale was changed by someone else while you were editing it. Reload it and save again.";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        var scale = await _planningDbContext.EstimationScales
            .Include(s => s.Versions)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (scale is null)
            return Result.Failure("Estimation scale not found.");

        var actor = EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId());
        var now = _dateTimeProvider.Now;

        var updateResult = scale.UpdateDetails(request.Name, request.Description, actor, now);
        if (updateResult.IsFailure)
            return updateResult;

        var publishResult = scale.PublishVersion(request.Values.Select(v => v.ToValue()), actor, now);
        if (publishResult.IsFailure)
            return publishResult;

        try
        {
            await _planningDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbUpdateErrors.IsUniqueViolation(ex))
        {
            // Another edit published the same version number first; these values were never compared with its.
            throw new ConflictException(VersionConflictError);
        }

        return Result.Success();
    }
}
