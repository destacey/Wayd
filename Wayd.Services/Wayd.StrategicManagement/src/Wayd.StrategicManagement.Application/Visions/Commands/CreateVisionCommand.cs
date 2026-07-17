using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Visions.Commands;

public sealed record CreateVisionCommand(string Description) : ICommand<ObjectIdAndKey>;

public sealed class CreateVisionCommandValidator : AbstractValidator<CreateVisionCommand>
{
    public CreateVisionCommandValidator()
    {
        RuleFor(v => v.Description)
            .NotEmpty()
            .MaximumLength(3072);
    }
}

public sealed class CreateVisionCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<CreateVisionCommandHandler> logger) : ICommandHandler<CreateVisionCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateVisionCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<CreateVisionCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateVisionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var vision = Vision.Create(request.Description);

            await _strategicManagementDbContext.Visions.AddAsync(vision, cancellationToken);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Vision {VisionId} created with Key {VisionKey}.", vision.Id, vision.Key);

            return new ObjectIdAndKey(vision.Id, vision.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}

