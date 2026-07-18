using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record CreateStrategicInitiativeCommand(string Name, string Description, LocalDateRange DateRange, Guid PortfolioId, List<Guid>? SponsorIds, List<Guid>? OwnerIds) : ICommand<ObjectIdAndKey>;

public sealed class CreateStrategicInitiativeCommandValidator : AbstractValidator<CreateStrategicInitiativeCommand>
{
    public CreateStrategicInitiativeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(x => x.DateRange)
            .NotNull();

        RuleFor(x => x.PortfolioId)
            .NotEmpty();

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");
    }
}

public sealed class CreateStrategicInitiativeCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateStrategicInitiativeCommandHandler> logger)
    : ICommandHandler<CreateStrategicInitiativeCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateStrategicInitiativeCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                    .Include(p => p.StrategicInitiatives)
                    .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", request.PortfolioId);
                return Result.Failure<ObjectIdAndKey>("Portfolio not found.");
            }

            var roles = GetRoles(request);

            var createResult = portfolio.CreateStrategicInitiative(
                request.Name,
                request.Description,
                request.DateRange,
                roles);
            if (createResult.IsFailure)
            {
                _logger.LogError("Error creating strategic initiative {StrategicInitiativeName} for Portfolio {PortfolioId}. Error message: {Error}", request.Name, request.PortfolioId, createResult.Error);
                return Result.Failure<ObjectIdAndKey>(createResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            var strategicInitiative = createResult.Value;

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} created with Key {StrategicInitiativeKey}.", strategicInitiative.Id, strategicInitiative.Key);

            return new ObjectIdAndKey(strategicInitiative.Id, strategicInitiative.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }

    private static Dictionary<StrategicInitiativeRole, HashSet<Guid>> GetRoles(CreateStrategicInitiativeCommand request)
    {
        Dictionary<StrategicInitiativeRole, HashSet<Guid>> roles = [];

        if (request.SponsorIds != null && request.SponsorIds.Count != 0)
        {
            roles.Add(StrategicInitiativeRole.Sponsor, [.. request.SponsorIds]);
        }
        if (request.OwnerIds != null && request.OwnerIds.Count != 0)
        {
            roles.Add(StrategicInitiativeRole.Owner, [.. request.OwnerIds]);
        }

        return roles;
    }
}