namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Unlinks a team or team of teams from a product. Succeeds, changing nothing, when it was not linked.
/// </summary>
public sealed record UnlinkProductTeamCommand(Guid Id, Guid TeamId) : ICommand;

public sealed class UnlinkProductTeamCommandValidator : AbstractValidator<UnlinkProductTeamCommand>
{
    public UnlinkProductTeamCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.TeamId)
            .NotEmpty();
    }
}

public sealed class UnlinkProductTeamCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UnlinkProductTeamCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UnlinkProductTeamCommand>
{
    private const string AppRequestName = nameof(UnlinkProductTeamCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UnlinkProductTeamCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UnlinkProductTeamCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .Include(p => p.TeamLinks)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            var unlinkResult = product.UnlinkTeam(
                request.TeamId,
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (unlinkResult.IsFailure)
            {
                product.ClearDomainEvents();

                _logger.LogInformation("Unable to unlink Team {TeamId} from Product {ProductId}. Error message: {Error}", request.TeamId, request.Id, unlinkResult.Error);
                return Result.Failure(unlinkResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Team {TeamId} unlinked from Product {ProductId}.", request.TeamId, request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
