using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record UpdateScoringModelCriterionCommand(
    Guid ScoringModelId,
    Guid CriterionId,
    string Name,
    string Token,
    string? Description,
    decimal? Weight,
    Guid? ScaleId)
    : ICommand;

public sealed class UpdateScoringModelCriterionCommandValidator : AbstractValidator<UpdateScoringModelCriterionCommand>
{
    public UpdateScoringModelCriterionCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.CriterionId)
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

internal sealed class UpdateScoringModelCriterionCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<UpdateScoringModelCriterionCommandHandler> logger)
    : ICommandHandler<UpdateScoringModelCriterionCommand>
{
    private const string AppRequestName = nameof(UpdateScoringModelCriterionCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateScoringModelCriterionCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateScoringModelCriterionCommand request, CancellationToken cancellationToken)
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
                return Result.Failure("Scoring Model not found.");
            }

            var updateResult = model.UpdateCriterion(request.CriterionId, request.Name, request.Token, request.Description, request.Weight, request.ScaleId);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update criterion on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Criterion {CriterionId} updated on Scoring Model {ScoringModelId}.", request.CriterionId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
