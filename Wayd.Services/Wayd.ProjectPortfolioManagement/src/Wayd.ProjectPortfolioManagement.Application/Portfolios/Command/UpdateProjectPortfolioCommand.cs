using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record UpdateProjectPortfolioCommand(Guid Id, string Name, string Description, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds) : ICommand;

public sealed class UpdateProjectPortfolioCommandValidator : AbstractValidator<UpdateProjectPortfolioCommand>
{
    public UpdateProjectPortfolioCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024);

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");

        RuleFor(x => x.ManagerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("ManagerIds cannot contain empty GUIDs.");
    }
}

public sealed class UpdateProjectPortfolioCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateProjectPortfolioCommandHandler> logger)
    : ICommandHandler<UpdateProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(UpdateProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateProjectPortfolioCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                .Include(p => p.Roles)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (portfolio is null)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} not found.", request.Id);
                return Result.Failure("Project Portfolio not found.");
            }

            var updateResult = portfolio.UpdateDetails(
                request.Name,
                request.Description
                );
            if (updateResult.IsFailure)
            {
                return await HandleDomainFailure(portfolio, updateResult, cancellationToken);
            }

            var roles = GetRoles(request);
            var updateRolesResult = portfolio.UpdateRoles(roles);
            if (updateRolesResult.IsFailure)
            {
                return await HandleDomainFailure(portfolio, updateRolesResult, cancellationToken);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private static Dictionary<ProjectPortfolioRole, HashSet<Guid>> GetRoles(UpdateProjectPortfolioCommand request)
    {
        Dictionary<ProjectPortfolioRole, HashSet<Guid>> roles = [];

        if (request.SponsorIds != null && request.SponsorIds.Count != 0)
        {
            roles.Add(ProjectPortfolioRole.Sponsor, [.. request.SponsorIds]);
        }
        if (request.OwnerIds != null && request.OwnerIds.Count != 0)
        {
            roles.Add(ProjectPortfolioRole.Owner, [.. request.OwnerIds]);
        }
        if (request.ManagerIds != null && request.ManagerIds.Count != 0)
        {
            roles.Add(ProjectPortfolioRole.Manager, [.. request.ManagerIds]);
        }

        return roles;
    }

    private async Task<Result> HandleDomainFailure(ProjectPortfolio portfolio, Result errorResult, CancellationToken cancellationToken)
    {
        // Reset the entity
        await _projectPortfolioManagementDbContext.Entry(portfolio).ReloadAsync(cancellationToken);
        portfolio.ClearDomainEvents();

        _logger.LogError("Unable to update Project Portfolio {ProjectPortfolioId}.  Error message: {Error}", portfolio.Id, errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
