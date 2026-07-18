using Wayd.Planning.Application.Risks.Dtos;

namespace Wayd.Planning.Application.Risks.Commands;

public sealed record ImportRisksCommand : ICommand
{
    public ImportRisksCommand(IEnumerable<ImportRiskDto> risks)
    {
        Risks = risks.ToList();
    }

    public List<ImportRiskDto> Risks { get; }
}

public sealed class ImportRisksCommandValidator : CustomValidator<ImportRisksCommand>
{
    public ImportRisksCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Risks)
            .NotNull()
            .NotEmpty();

        RuleForEach(e => e.Risks)
            .NotNull()
            .SetValidator(new ImportRiskDtoValidator(dateTimeProvider));
    }
}

public sealed class ImportRisksCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ILogger<ImportRisksCommandHandler> logger) : ICommandHandler<ImportRisksCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportRisksCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportRisksCommand request, CancellationToken cancellationToken)
    {
        // TODO: allow individual records to fail and return a list of errors

        try
        {
            foreach (var importedRisk in request.Risks)
            {
                var risk = Risk.Import(
                    importedRisk.Summary,
                    importedRisk.Description,
                    importedRisk.TeamId,
                    importedRisk.ReportedOn,
                    importedRisk.ReportedById,
                    importedRisk.Status,
                    importedRisk.Category,
                    importedRisk.Impact,
                    importedRisk.Likelihood,
                    importedRisk.AssigneeId,
                    importedRisk.FollowUpDate,
                    importedRisk.Response,
                    importedRisk.ClosedDate
                    );

                await _planningDbContext.Risks.AddAsync(risk, cancellationToken);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<Guid>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
