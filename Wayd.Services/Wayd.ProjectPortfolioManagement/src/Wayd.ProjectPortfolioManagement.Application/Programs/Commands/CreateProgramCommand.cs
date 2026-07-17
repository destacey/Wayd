using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record CreateProgramCommand(string Name, string Description, LocalDateRange? DateRange, Guid PortfolioId, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds, List<Guid>? StrategicThemeIds) : ICommand<ObjectIdAndKey>;

public sealed class CreateProgramCommandValidator : AbstractValidator<CreateProgramCommand>
{
    public CreateProgramCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(x => x.PortfolioId)
            .NotEmpty();

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");

        RuleFor(x => x.ManagerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("ManagerIds cannot contain empty GUIDs.");

        RuleFor(x => x.StrategicThemeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("StrategicThemeIds cannot contain empty GUIDs.");
    }
}

public sealed class CreateProgramCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<CreateProgramCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateProgramCommand, ObjectIdAndKey>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<CreateProgramCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateProgramCommand request, CancellationToken cancellationToken)
    {
        var strategicThemeIds = request.StrategicThemeIds?.ToHashSet() ?? [];
        var strategicThemes = request.StrategicThemeIds is not null && request.StrategicThemeIds.Count != 0
            ? await _ppmDbContext.PpmStrategicThemes
                .Where(st => strategicThemeIds.Contains(st.Id))
                .ToListAsync(cancellationToken)
            : [];
        if (request.StrategicThemeIds is not null && strategicThemes.Count != strategicThemeIds.Count)
        {
            _logger.LogInformation("One or more Strategic Themes not found.");
            return Result.Failure<ObjectIdAndKey>("One or more Strategic Themes not found.");
        }
        else if (request.StrategicThemeIds is not null && strategicThemes.Any(st => st.State != StrategicThemeState.Active))
        {
            _logger.LogInformation("One or more Strategic Themes are not active.");
            return Result.Failure<ObjectIdAndKey>("One or more Strategic Themes are not active.");
        }

        var portfolio = await _ppmDbContext.Portfolios
                .Include(p => p.Programs)
                .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
        if (portfolio == null)
        {
            _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure<ObjectIdAndKey>("Portfolio not found.");
        }

        var roles = GetRoles(request);

        var createResult = portfolio.CreateProgram(
                request.Name,
                request.Description,
                request.DateRange,
                roles,
                [.. strategicThemes.Select(st => st.Id)],
                _dateTimeProvider.Now
                );
        if (createResult.IsFailure)
        {
            _logger.LogError("Error creating program {ProgramName} for Portfolio {PortfolioId}. Error message: {Error}", request.Name, request.PortfolioId, createResult.Error);
            return Result.Failure<ObjectIdAndKey>(createResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        var program = createResult.Value;

        _logger.LogInformation("Program {ProgramId} created with Key {ProgramKey}.", program.Id, program.Key);

        return Result.Success(new ObjectIdAndKey(program.Id, program.Key));
    }

    private static Dictionary<ProgramRole, HashSet<Guid>> GetRoles(CreateProgramCommand request)
    {
        Dictionary<ProgramRole, HashSet<Guid>> roles = [];

        if (request.SponsorIds != null && request.SponsorIds.Count != 0)
        {
            roles.Add(ProgramRole.Sponsor, [.. request.SponsorIds]);
        }
        if (request.OwnerIds != null && request.OwnerIds.Count != 0)
        {
            roles.Add(ProgramRole.Owner, [.. request.OwnerIds]);
        }
        if (request.ManagerIds != null && request.ManagerIds.Count != 0)
        {
            roles.Add(ProgramRole.Manager, [.. request.ManagerIds]);
        }

        return roles;
    }
}
