using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Visions.Commands;

public sealed record ArchiveVisionCommand(Guid Id) : ICommand;

public sealed class ArchiveVisionCommandValidator : AbstractValidator<ArchiveVisionCommand>
{
    public ArchiveVisionCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveVisionCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<ArchiveVisionCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ArchiveVisionCommand>
{
    private const string AppRequestName = nameof(ArchiveVisionCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<ArchiveVisionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ArchiveVisionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var visions = await _strategicManagementDbContext.Visions
                .ToListAsync(cancellationToken);
            if (visions.Count == 0)
            {
                _logger.LogInformation("No Visions found.");
                return Result.Failure("No Visions found.");
            }

            var vision = visions.SingleOrDefault(v => v.Id == request.Id);
            if (vision is null)
            {
                _logger.LogInformation("Vision {VisionId} not found.", request.Id);
                return Result.Failure("Vision not found.");
            }

            var aggregate = new VisionAggregate(visions);

            var archiveResult = aggregate.Archive(request.Id, _dateTimeProvider.Now);
            if (archiveResult.IsFailure)
            {
                // Reset the entity
                await _strategicManagementDbContext.Entry(vision).ReloadAsync(cancellationToken);
                vision.ClearDomainEvents();

                _logger.LogError("Unable to archive Vision {VisionId}.  Error message: {Error}", request.Id, archiveResult.Error);
                return Result.Failure(archiveResult.Error);
            }
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Vision {VisionId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}