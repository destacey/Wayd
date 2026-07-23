using Wayd.Common.Application.Validation;
using Wayd.StrategicManagement.Application.StrategicThemes.Dtos;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

/// <summary>
/// Additively imports a batch of strategic themes. Themes are the natural-key anchor other imports resolve
/// against (programs and projects reference them by name), so the batch is all-or-nothing and rejects any
/// name that is duplicated within the batch or already exists — a silently reused name would attach later
/// rows to the wrong theme. Each theme is created through the domain factory and persisted with a single
/// SaveChanges so the creation events fire and replicate into the PPM projection.
/// </summary>
public sealed record ImportStrategicThemesCommand : ICommand
{
    public ImportStrategicThemesCommand(IEnumerable<ImportStrategicThemeDto> strategicThemes)
    {
        StrategicThemes = [.. strategicThemes];
    }

    public List<ImportStrategicThemeDto> StrategicThemes { get; }
}

public sealed class ImportStrategicThemesCommandValidator : CustomValidator<ImportStrategicThemesCommand>
{
    public ImportStrategicThemesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.StrategicThemes)
            .NotNull()
            .NotEmpty();

        RuleForEach(t => t.StrategicThemes)
            .NotNull()
            .SetValidator(new ImportStrategicThemeDtoValidator());
    }
}

public sealed class ImportStrategicThemesCommandHandler(
    IStrategicManagementDbContext strategicManagementDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ImportStrategicThemesCommandHandler> logger) : ICommandHandler<ImportStrategicThemesCommand>
{
    private const string RequestName = nameof(ImportStrategicThemesCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ImportStrategicThemesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportStrategicThemesCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        try
        {
            var duplicates = request.StrategicThemes
                .GroupBy(t => Normalize(t.Name), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following strategic theme names appear more than once in the import: {Quote(duplicates)}.");

            var names = request.StrategicThemes.Select(t => Normalize(t.Name)).ToList();

            var existing = await _strategicManagementDbContext.StrategicThemes
                .Where(t => names.Contains(t.Name))
                .Select(t => t.Name)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
                return Fail($"The following strategic themes already exist: {Quote(existing)}.");

            foreach (var row in request.StrategicThemes)
            {
                var theme = StrategicTheme.Create(Normalize(row.Name), row.Description.Trim(), row.State, timestamp);

                await _strategicManagementDbContext.StrategicThemes.AddAsync(theme, cancellationToken);
            }

            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: imported {Count} strategic theme(s).", RequestName, request.StrategicThemes.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private static string Normalize(string name) => name.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
