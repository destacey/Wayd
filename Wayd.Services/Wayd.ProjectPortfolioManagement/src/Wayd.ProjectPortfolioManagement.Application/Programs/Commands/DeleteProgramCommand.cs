
namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record DeleteProgramCommand(Guid Id) : ICommand;

public sealed class DeleteProgramCommandValidator : AbstractValidator<DeleteProgramCommand>
{
    public DeleteProgramCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProgramCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<DeleteProgramCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteProgramCommand>
{
    private const string AppRequestName = nameof(DeleteProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<DeleteProgramCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteProgramCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var program = await _ppmDbContext.Programs
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .Include(p => p.Projects)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (program is null)
            {
                _logger.LogInformation("Program {ProgramId} not found.", request.Id);
                return Result.Failure("Program not found.");
            }

            var portfolio = await _ppmDbContext.Portfolios
                    .Include(p => p.Programs.Where(p => p.Id == request.Id))
                    .FirstOrDefaultAsync(p => p.Id == program.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", program.PortfolioId);
                return Result.Failure("Portfolio not found.");
            }

            var deleteResult = portfolio.DeleteProgram(program.Id, _dateTimeProvider.Now);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Error deleting program {ProgramId}. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Program {ProgramId} deleted. Key: {ProgramKey}, Name: {ProgramName}", program.Id, program.Key, program.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

