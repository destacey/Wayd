using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record ActivateProgramCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ActivateProgramCommandValidator : AbstractValidator<ActivateProgramCommand>
{
    public ActivateProgramCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateProgramCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ILogger<ActivateProgramCommandHandler> logger) : ICommandHandler<ActivateProgramCommand>
{
    private const string AppRequestName = nameof(ActivateProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<ActivateProgramCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateProgramCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(cancellationToken);

            var program = await _projectPortfolioManagementDbContext.Programs
                .AsSplitQuery()
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (program is null)
            {
                _logger.LogInformation("Program {ProgramId} not found.", request.Id);
                return Result.Failure("Program not found.");
            }

            var activateResult = program.Activate(actor, program.AncestryRoles());
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(program).ReloadAsync(cancellationToken);

                program.ClearDomainEvents();

                _logger.LogError("Unable to activate Program {ProgramId}.  Error message: {Error}", request.Id, activateResult.Error);

                return Result.Failure(activateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Program {ProgramId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
