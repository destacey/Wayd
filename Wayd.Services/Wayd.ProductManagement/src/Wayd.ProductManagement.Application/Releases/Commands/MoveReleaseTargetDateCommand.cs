namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Moves a release's target date.
/// </summary>
/// <remarks>
/// Its own command rather than part of an edit, because a slipped date is a fact worth recording on
/// its own — the event carries both ends so "slipped two weeks" stays answerable.
/// </remarks>
public sealed record MoveReleaseTargetDateCommand(Guid Id, LocalDate? TargetDate) : ICommand, IRequireLinkedEmployee;

public sealed class MoveReleaseTargetDateCommandValidator : AbstractValidator<MoveReleaseTargetDateCommand>
{
    public MoveReleaseTargetDateCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class MoveReleaseTargetDateCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<MoveReleaseTargetDateCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<MoveReleaseTargetDateCommand>
{
    private const string AppRequestName = nameof(MoveReleaseTargetDateCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<MoveReleaseTargetDateCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(MoveReleaseTargetDateCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var release = await _productManagementDbContext.Releases
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (release is null)
            {
                _logger.LogInformation("Release {ReleaseId} not found.", request.Id);
                return Result.Failure("Release not found.");
            }

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var result = release.MoveTargetDate(
                request.TargetDate,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now);

            if (result.IsFailure)
            {
                release.ClearDomainEvents();

                _logger.LogInformation(
                    "Unable to move Release {ReleaseId} target date. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} target date moved.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
