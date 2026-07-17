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
    ILogger<UpdateStrategicInitiativeCommandHandler> logger)
    : ICommandHandler<UpdateStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(UpdateStrategicInitiativeCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateStrategicInitiativeCommandHandler> _logger = logger;
    public async Task<Result> Handle(UpdateStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(s => s.Roles)
                .Include(s => s.Kpis)
                .Include(s => s.StrategicInitiativeProjects)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (strategicInitiative == null)
            {
                _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.Id);
                return Result.Failure("Strategic Initiative not found.");
            }

            var updateResult = strategicInitiative.UpdateDetails(request.Name, request.Description, request.DateRange);
            if (updateResult.IsFailure)
            {
                return await HandleDomainFailure(strategicInitiative, updateResult, cancellationToken);
            }

            var roles = GetRoles(request);
            var updateRolesResult = strategicInitiative.UpdateRoles(roles);
            if (updateRolesResult.IsFailure)
            {
                return await HandleDomainFailure(strategicInitiative, updateRolesResult, cancellationToken);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} updated successfully.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{AppRequestName} failed.", AppRequestName);
            return Result.Failure("An error occurred while updating the strategic initiative.");
        }
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

    private async Task<Result> HandleDomainFailure(StrategicInitiative strategicInitiative, Result errorResult, CancellationToken cancellationToken)
    {
        // Reset the entity
        await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);
        strategicInitiative.ClearDomainEvents();

        _logger.LogError("Unable to update strategic initiative {StrategicInitiativeId}.  Error message: {Error}", strategicInitiative.Id, errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
