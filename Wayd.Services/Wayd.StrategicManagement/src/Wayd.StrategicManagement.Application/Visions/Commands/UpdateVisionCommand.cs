namespace Wayd.StrategicManagement.Application.Visions.Commands;

public sealed record UpdateVisionCommand(Guid Id, string Description) : ICommand;

public sealed class UpdateVisionCommandValidator : AbstractValidator<UpdateVisionCommand>
{
    public UpdateVisionCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.Description)
            .NotEmpty()
            .MaximumLength(3072);
    }
}

public sealed class UpdateVisionCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<UpdateVisionCommandHandler> logger) : ICommandHandler<UpdateVisionCommand>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<UpdateVisionCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateVisionCommand request, CancellationToken cancellationToken)
    {
        var vision = await _strategicManagementDbContext.Visions
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (vision is null)
        {
            _logger.LogInformation("Vision {VisionId} not found.", request.Id);
            return Result.Failure("Vision not found.");
        }
        var updateResult = vision.Update(request.Description);

        if (updateResult.IsFailure)
        {
            _logger.LogInformation("Unable to update Vision {VisionId}.  Error message: {Error}", request.Id, updateResult.Error);
            return Result.Failure(updateResult.Error);
        }

        await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vision {VisionId} updated.", request.Id);

        return Result.Success();
    }
}
