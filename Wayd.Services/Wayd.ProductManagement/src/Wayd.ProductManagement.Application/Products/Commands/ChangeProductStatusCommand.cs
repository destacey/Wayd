using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>Moves a product to another status in the workflow governing it.</summary>
/// <param name="Reason">
/// Why the move was made, recorded on the status transition. Required when the workflow's move has a
/// reason condition; optional otherwise.
/// </param>
public sealed record ChangeProductStatusCommand(Guid Id, Guid StatusId, string? Reason = null) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeProductStatusCommandValidator : AbstractValidator<ChangeProductStatusCommand>
{
    public ChangeProductStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.StatusId)
            .NotEmpty();

        RuleFor(x => x.Reason)
            .MaximumLength(1024);
    }
}

public sealed class ChangeProductStatusCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ITransitionConditionChecker conditionChecker,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<ChangeProductStatusCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProductStatusCommand>
{
    private const string AppRequestName = nameof(ChangeProductStatusCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ITransitionConditionChecker _conditionChecker = conditionChecker;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
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

            // TargetRef carries the moves the workflow allows into the status, and what each needs, which
            // the aggregate enforces.
            var target = workflow.Value.TargetRef(request.StatusId);

            if (target is null)
            {
                _logger.LogInformation(
                    "Status {StatusId} does not belong to workflow {WorkflowId}.", request.StatusId, workflow.Value.Id);
                return Result.Failure($"That status does not belong to '{workflow.Value.Name}'.");
            }

            // The conditions the product cannot check itself are decided here; it checks the rest.
            var status = await _conditionChecker.Evaluate(
                ProductWorkflowOwners.Product.Key, product.Id, product.StatusId, target, cancellationToken);

            // Read per scope rather than from the claim snapshot, which a personal access token
            // freezes for its whole lifetime. This value is frozen onto the transition, so a stale
            // one would misattribute the change permanently.
            var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

            var changeResult = product.ChangeStatus(
                status,
                EventActor.User(_currentUser.GetUserId(), employeeId),
                _dateTimeProvider.Now,
                request.Reason);

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
