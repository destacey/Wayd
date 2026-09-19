using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Releases.Commands;

/// <summary>
/// Permanently deletes a release, its contents list and its status history.
/// </summary>
/// <remarks>
/// The versions and packages it listed are separate records and stay — deleting a release is also how a
/// package in an announced release, whose contents are fixed, becomes deletable. Withdrawing keeps the
/// record of what was announced, and is the everyday way out.
/// </remarks>
public sealed record DeleteReleaseCommand(Guid Id) : ICommand;

public sealed class DeleteReleaseCommandValidator : AbstractValidator<DeleteReleaseCommand>
{
    public DeleteReleaseCommandValidator()
    {
        RuleFor(r => r.Id)
            .NotEmpty();
    }
}

public sealed class DeleteReleaseCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusWorkflowDbContext statusWorkflowDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<DeleteReleaseCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteReleaseCommand>
{
    private const string AppRequestName = nameof(DeleteReleaseCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteReleaseCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteReleaseCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The contents cascade in the database, but are loaded so the tracked graph matches.
            var release = await _productManagementDbContext.Releases
                .Include(r => r.Versions)
                .Include(r => r.Packages)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (release is null)
            {
                _logger.LogInformation("Release {ReleaseId} not found.", request.Id);
                return Result.Failure("Release not found.");
            }

            await StatusHistoryRemoval.Stage(
                _statusWorkflowDbContext,
                ProductWorkflowOwners.Release.Key,
                [release.Id],
                cancellationToken);

            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            release.Delete(EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
            _productManagementDbContext.Releases.Remove(release);

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Release {ReleaseId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
