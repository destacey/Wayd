using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Products.Commands;

public sealed record ChangeProductStatusCommand(Guid Id, Guid StatusId) : ICommand;

public sealed class ChangeProductStatusCommandValidator : AbstractValidator<ChangeProductStatusCommand>
{
    public ChangeProductStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.StatusId)
            .NotEmpty();
    }
}

public sealed class ChangeProductStatusCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<ChangeProductStatusCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProductStatusCommand>
{
    private const string AppRequestName = nameof(ChangeProductStatusCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeProductStatusCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeProductStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productManagementDbContext.Products
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure("Product not found.");
            }

            // Resolved through the governing workflow rather than by loading the status directly: that
            // is what stops a caller moving a product to a status belonging to some other workflow.
            var workflow = await _statusResolver.ForScope(
                ProductWorkflowOwners.Product.Key,
                scopeId: null,
                cancellationToken);

            if (workflow.IsFailure)
            {
                _logger.LogError("Unable to resolve the product workflow. Error message: {Error}", workflow.Error);
                return Result.Failure(workflow.Error);
            }

            var status = workflow.Value.Statuses.FirstOrDefault(s => s.Id == request.StatusId);

            if (status is null)
            {
                _logger.LogInformation(
                    "Status {StatusId} does not belong to workflow {WorkflowId}.", request.StatusId, workflow.Value.Id);
                return Result.Failure($"That status does not belong to '{workflow.Value.Name}'.");
            }

            var changeResult = product.ChangeStatus(
                StatusRef.From(status),
                EventActor.User(_currentUser.GetUserId()),
                _dateTimeProvider.Now);

            if (changeResult.IsFailure)
            {
                // No reload: every refusal on this aggregate is checked before any state is
                // touched, so there is nothing to roll back.
                product.ClearDomainEvents();

                _logger.LogError("Unable to change status for Product {ProductId}. Error message: {Error}", request.Id, changeResult.Error);
                return Result.Failure(changeResult.Error);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Product {ProductId} moved to status {StatusId}.", request.Id, request.StatusId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
