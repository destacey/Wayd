using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record CreateProjectPortfolioCommand(string Name, string Description, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds) : ICommand<ObjectIdAndKey>;

public sealed class CreateProjectPortfolioCommandValidator : AbstractValidator<CreateProjectPortfolioCommand>
{
    public CreateProjectPortfolioCommandValidator()
    {
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

public sealed class CreateProjectPortfolioCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateProjectPortfolioCommandHandler> logger)
    : ICommandHandler<CreateProjectPortfolioCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateProjectPortfolioCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var roles = GetRoles(request);

            var portfolio = ProjectPortfolio.Create(
                request.Name,
                request.Description,
                roles
                );

            await _projectPortfolioManagementDbContext.Portfolios.AddAsync(portfolio, cancellationToken);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} created with Key {ProjectPortfolioKey}.", portfolio.Id, portfolio.Key);

            return new ObjectIdAndKey(portfolio.Id, portfolio.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }

    private static Dictionary<ProjectPortfolioRole, HashSet<Guid>> GetRoles(CreateProjectPortfolioCommand request)
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
}
