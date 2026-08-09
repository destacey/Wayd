using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record CancelProgramCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class CancelProgramCommandValidator : AbstractValidator<CancelProgramCommand>
{
    public CancelProgramCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CancelProgramCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ILogger<CancelProgramCommandHandler> logger) : ICommandHandler<CancelProgramCommand>
{
    private const string AppRequestName = nameof(CancelProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<CancelProgramCommandHandler> _logger = logger;

    public async Task<Result> Handle(CancelProgramCommand request, CancellationToken cancellationToken)
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

            var cancelResult = program.Cancel(actor, program.AncestryRoles());
            if (cancelResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(program).ReloadAsync(cancellationToken);

                program.ClearDomainEvents();

                _logger.LogError("Unable to cancel Program {ProgramId}.  Error message: {Error}", request.Id, cancelResult.Error);

                return Result.Failure(cancelResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Program {ProgramId} canceled.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
