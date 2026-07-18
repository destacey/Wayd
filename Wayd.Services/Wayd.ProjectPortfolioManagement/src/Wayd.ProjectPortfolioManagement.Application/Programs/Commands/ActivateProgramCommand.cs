namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record ActivateProgramCommand(Guid Id) : ICommand;

public sealed class ActivateProgramCommandValidator : AbstractValidator<ActivateProgramCommand>
{
    public ActivateProgramCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateProgramCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ActivateProgramCommandHandler> logger) : ICommandHandler<ActivateProgramCommand>
{
    private const string AppRequestName = nameof(ActivateProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateProgramCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateProgramCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var program = await _projectPortfolioManagementDbContext.Programs
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (program is null)
            {
                _logger.LogInformation("Program {ProgramId} not found.", request.Id);
                return Result.Failure("Program not found.");
            }

            var activateResult = program.Activate();
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
