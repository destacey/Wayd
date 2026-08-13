using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Programs.Commands;

public sealed record UpdateProgramCommand(Guid Id, string Name, string Description, LocalDateRange? DateRange, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds, List<Guid>? StrategicThemeIds) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateProgramCommandValidator : AbstractValidator<UpdateProgramCommand>
{
    public UpdateProgramCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2048);

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

public sealed class UpdateProgramCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<UpdateProgramCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateProgramCommand>
{
    private const string AppRequestName = nameof(UpdateProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProgramCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateProgramCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            // Portfolio roles are loaded because delivery leadership inherits downward — an Owner/Manager
            // on the parent portfolio may manage this program even without a role on the program itself.
            var program = await _ppmDbContext.Programs
                .AsSplitQuery()
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (program == null)
            {
                _logger.LogInformation("Program with Id {ProgramId} not found.", request.Id);
                return Result.Failure("Program not found.");
            }

            var ancestry = program.AncestryRoles();

            var updateResult = program.UpdateDetails(
                actor,
                ancestry,
                request.Name,
                request.Description,
                _dateTimeProvider.Now);
            if (updateResult.IsFailure)
            {
                return await HandleDomainFailure(program, updateResult, cancellationToken);
            }

            var updateTimelineResult = program.UpdateTimeline(actor, ancestry, request.DateRange);
            if (updateTimelineResult.IsFailure)
            {
                return await HandleDomainFailure(program, updateTimelineResult, cancellationToken);
            }

            var roles = GetRoles(request);
            var updateRolesResult = program.UpdateRoles(actor, ancestry, roles);
            if (updateRolesResult.IsFailure)
            {
                return await HandleDomainFailure(program, updateRolesResult, cancellationToken);
            }

            var strategicThemes = request.StrategicThemeIds?.ToHashSet() ?? [];
            var updateStrategicThemesResult = program.UpdateStrategicThemes(strategicThemes);
            if (updateStrategicThemesResult.IsFailure)
            {
                return await HandleDomainFailure(program, updateStrategicThemesResult, cancellationToken);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Program {ProgramId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private static Dictionary<ProgramRole, HashSet<Guid>> GetRoles(UpdateProgramCommand request)
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

    private async Task<Result> HandleDomainFailure(Program program, Result errorResult, CancellationToken cancellationToken)
    {
        // Reset the entity
        await _ppmDbContext.Entry(program).ReloadAsync(cancellationToken);
        program.ClearDomainEvents();

        _logger.LogError("Unable to update Program {ProgramId}.  Error message: {Error}", program.Id, errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
