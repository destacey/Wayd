namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record CompleteProgramCommand(Guid Id) : ICommand;

public sealed class CompleteProgramCommandValidator : AbstractValidator<CompleteProgramCommand>
{
    public CompleteProgramCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CompleteProgramCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CompleteProgramCommandHandler> logger) : ICommandHandler<CompleteProgramCommand>
{
    private const string AppRequestName = nameof(CompleteProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CompleteProgramCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompleteProgramCommand request, CancellationToken cancellationToken)
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

            var completeResult = program.Complete();
            if (completeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(program).ReloadAsync(cancellationToken);

                program.ClearDomainEvents();

                _logger.LogError("Unable to complete Program {ProgramId}.  Error message: {Error}", request.Id, completeResult.Error);

                return Result.Failure(completeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Program {ProgramId} completed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
