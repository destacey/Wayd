using Wayd.Common.Application.Models;

namespace Wayd.StrategicManagement.Application.Visions.Commands;

public sealed record DeleteVisionCommand(Guid Id) : ICommand;
public sealed class DeleteVisionCommandValidator : AbstractValidator<DeleteVisionCommand>
{
    public DeleteVisionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteVisionCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<DeleteVisionCommandHandler> logger) : ICommandHandler<DeleteVisionCommand>
{
    private const string AppRequestName = nameof(DeleteVisionCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<DeleteVisionCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteVisionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicTheme = await _strategicManagementDbContext.Visions
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (strategicTheme is null)
            {
                _logger.LogInformation("Vision {VisionId} not found.", request.Id);
                return Result.Failure("Vision not found.");
            }

            _strategicManagementDbContext.Visions.Remove(strategicTheme);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Vision {VisionId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
