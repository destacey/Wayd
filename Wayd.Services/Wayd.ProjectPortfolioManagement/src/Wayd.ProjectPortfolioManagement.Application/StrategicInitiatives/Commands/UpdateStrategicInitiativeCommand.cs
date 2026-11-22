using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record UpdateStrategicInitiativeCommand(Guid Id, string Name, string Description, LocalDateRange DateRange, List<Guid>? SponsorIds, List<Guid>? OwnerIds) : ICommand;

public sealed class UpdateStrategicInitiativeCommandValidator : AbstractValidator<UpdateStrategicInitiativeCommand>
{
    public UpdateStrategicInitiativeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(x => x.DateRange)
            .NotNull();

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");
    }
}

public sealed class UpdateStrategicInitiativeCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateStrategicInitiativeCommandHandler> logger,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateStrategicInitiativeCommand>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateStrategicInitiativeCommandHandler> _logger = logger;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
            .Include(s => s.Roles)
            .Include(s => s.StrategicInitiativeProjects)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (strategicInitiative == null)
        {
            _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.Id);
            return Result.Failure("Strategic Initiative not found.");
        }

        var actor = EventActor.User(_currentUser.GetUserId());
        var timestamp = _dateTimeProvider.Now;

        var updateResult = strategicInitiative.UpdateDetails(request.Name, request.Description, actor, timestamp);
        if (updateResult.IsFailure)
        {
            return HandleDomainFailure(strategicInitiative, updateResult);
        }

        var timelineResult = strategicInitiative.UpdateTimeline(request.DateRange, actor, timestamp);
        if (timelineResult.IsFailure)
        {
            return HandleDomainFailure(strategicInitiative, timelineResult);
        }

        var roles = GetRoles(request);
        var peopleExist = await _projectPortfolioManagementDbContext.EnsureEmployeesExist(roles.Values.SelectMany(ids => ids), cancellationToken);
        if (peopleExist.IsFailure)
            return Result.Failure(peopleExist.Error);
        var updateRolesResult = strategicInitiative.UpdateRoles(roles, actor, timestamp);
        if (updateRolesResult.IsFailure)
        {
            return HandleDomainFailure(strategicInitiative, updateRolesResult);
        }

        await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} updated successfully.", request.Id);

        return Result.Success();
    }

    private static Dictionary<StrategicInitiativeRole, HashSet<Guid>> GetRoles(UpdateStrategicInitiativeCommand request)
    {
        var roles = new Dictionary<StrategicInitiativeRole, HashSet<Guid>>();

        if (request.SponsorIds != null)
        {
            roles[StrategicInitiativeRole.Sponsor] = [.. request.SponsorIds];
        }
        if (request.OwnerIds != null)
        {
            roles[StrategicInitiativeRole.Owner] = [.. request.OwnerIds];
        }

        return roles;
    }

    private Result HandleDomainFailure(StrategicInitiative strategicInitiative, Result errorResult)
    {
        _logger.LogError("Unable to update strategic initiative {StrategicInitiativeId}.  Error message: {Error}", strategicInitiative.Id, errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
