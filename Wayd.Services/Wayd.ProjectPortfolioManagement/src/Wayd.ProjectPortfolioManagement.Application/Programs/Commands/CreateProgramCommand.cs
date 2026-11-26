using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Common.Domain.Events;

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
    ICurrentUser currentUser,
    ILogger<CreateProgramCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateProgramCommand, ObjectIdAndKey>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreateProgramCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateProgramCommand request, CancellationToken cancellationToken)
    {
        var strategicThemeIds = request.StrategicThemeIds?.ToHashSet() ?? [];
        var themesAttachable = await _ppmDbContext.EnsureThemesAttachable(strategicThemeIds, alreadyAttached: null, cancellationToken);
        if (themesAttachable.IsFailure)
        {
            _logger.LogInformation("Strategic themes refused for a new program: {Error}", themesAttachable.Error);
            return Result.Failure<ObjectIdAndKey>(themesAttachable.Error);
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
        var peopleExist = await _ppmDbContext.EnsureEmployeesExist(roles.Values.SelectMany(ids => ids), cancellationToken);
        if (peopleExist.IsFailure)
            return Result.Failure<ObjectIdAndKey>(peopleExist.Error);

        var createResult = portfolio.CreateProgram(
                request.Name,
                request.Description,
                request.DateRange,
                roles,
                strategicThemeIds,
                EventActor.User(_currentUser.GetUserId()),
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
