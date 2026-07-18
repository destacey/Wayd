using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record AddScoringModelCriterionCommand(
    Guid ScoringModelId,
    string Name,
    string Token,
    string? Description,
    decimal? Weight,
    Guid? ScaleId)
    : ICommand<Guid>;

public sealed class AddScoringModelCriterionCommandValidator : AbstractValidator<AddScoringModelCriterionCommand>
{
    public AddScoringModelCriterionCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(ScoringToken.MaxLength)
            .Must(ScoringToken.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid token.");

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class AddScoringModelCriterionCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<AddScoringModelCriterionCommandHandler> logger)
    : ICommandHandler<AddScoringModelCriterionCommand, Guid>
{
    private const string AppRequestName = nameof(AddScoringModelCriterionCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<AddScoringModelCriterionCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddScoringModelCriterionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Criteria)
                .Include(x => x.Outputs)
                .Include(x => x.Scales)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure<Guid>("Scoring Model not found.");
            }

            var addResult = model.AddCriterion(request.Name, request.Token, request.Description, request.Weight, request.ScaleId);
            if (addResult.IsFailure)
            {
                _logger.LogError("Unable to add criterion to Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Criterion {CriterionId} added to Scoring Model {ScoringModelId}.", addResult.Value.Id, request.ScoringModelId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
