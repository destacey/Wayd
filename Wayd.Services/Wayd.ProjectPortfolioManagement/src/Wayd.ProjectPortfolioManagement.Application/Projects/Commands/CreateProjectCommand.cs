using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Validators;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record CreateProjectCommand(string Name, string Description, string? BusinessCase, string? ExpectedBenefits, ProjectKey Key, int ExpenditureCategoryId, LocalDateRange? DateRange, Guid PortfolioId, Guid? ProgramId, Guid? ProjectLifecycleId, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds, List<Guid>? MemberIds, List<Guid>? StrategicThemeIds) : ICommand<ProjectIdAndKey>;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator(IProjectPortfolioManagementDbContext ppmDbContext)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(x => x.BusinessCase)
            .MaximumLength(4096);

        RuleFor(x => x.ExpectedBenefits)
            .MaximumLength(4096);

        RuleFor(x => x.Key)
            .NotEmpty()
            .SetValidator(x => new ProjectKeyValidator(ppmDbContext, null));

        RuleFor(x => x.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(x => x.PortfolioId)
            .NotEmpty();

        RuleFor(x => x.ProgramId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("ProgramId cannot be an empty GUID.");

        RuleFor(x => x.ProjectLifecycleId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("ProjectLifecycleId cannot be an empty GUID.");

        RuleFor(x => x.SponsorIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("SponsorIds cannot contain empty GUIDs.");

        RuleFor(x => x.OwnerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("OwnerIds cannot contain empty GUIDs.");

        RuleFor(x => x.ManagerIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("ManagerIds cannot contain empty GUIDs.");

        RuleFor(x => x.MemberIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("MemberIds cannot contain empty GUIDs.");

        RuleFor(x => x.StrategicThemeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("StrategicThemeIds cannot contain empty GUIDs.");
    }
}

public sealed class CreateProjectCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateProjectCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateProjectCommand, ProjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateProjectCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ProjectIdAndKey>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: move the Active state checks into the domain models
            var expenditureCategory = await _projectPortfolioManagementDbContext.ExpenditureCategories
                .FirstOrDefaultAsync(e => e.Id == request.ExpenditureCategoryId, cancellationToken);
            if (expenditureCategory == null)
            {
                _logger.LogInformation("Expenditure Category with Id {ExpenditureCategoryId} not found.", request.ExpenditureCategoryId);
                return Result.Failure<ProjectIdAndKey>("Expenditure Category not found.");
            }
            else if (expenditureCategory.State != ExpenditureCategoryState.Active)
            {
                _logger.LogInformation("Expenditure Category with Id {ExpenditureCategoryId} is not active.", request.ExpenditureCategoryId);
                return Result.Failure<ProjectIdAndKey>("Expenditure Category is not active.");
            }

            var strategicThemeIds = request.StrategicThemeIds?.ToHashSet() ?? [];
            var strategicThemes = request.StrategicThemeIds is not null && request.StrategicThemeIds.Count != 0
                ? await _projectPortfolioManagementDbContext.PpmStrategicThemes
                    .Where(st => strategicThemeIds.Contains(st.Id))
                    .ToListAsync(cancellationToken)
                : [];
            if (request.StrategicThemeIds is not null && strategicThemes.Count != strategicThemeIds.Count)
            {
                _logger.LogInformation("One or more Strategic Themes not found.");
                return Result.Failure<ProjectIdAndKey>("One or more Strategic Themes not found.");
            }
            else if (request.StrategicThemeIds is not null && strategicThemes.Any(st => st.State != StrategicThemeState.Active))
            {
                _logger.LogInformation("One or more Strategic Themes are not active.");
                return Result.Failure<ProjectIdAndKey>("One or more Strategic Themes are not active.");
            }

            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                    .Include(p => p.Programs)
                    .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", request.PortfolioId);
                return Result.Failure<ProjectIdAndKey>("Portfolio not found.");
            }

            var roles = GetRoles(request);

            var projectKey = new ProjectKey(request.Key);

            // Cheap scalar so the new project can be ranked at the bottom without loading every
            // sibling project into the aggregate.
            var currentMaxRank = await _projectPortfolioManagementDbContext.Projects
                .Where(p => p.PortfolioId == request.PortfolioId)
                .MaxAsync(p => (double?)p.Rank, cancellationToken);

            var createResult = portfolio.CreateProject(
                request.Name,
                request.Description,
                projectKey,
                request.ExpenditureCategoryId,
                request.DateRange,
                request.ProgramId,
                request.BusinessCase,
                request.ExpectedBenefits,
                roles,
                [.. strategicThemes.Select(st => st.Id)],
                _dateTimeProvider.Now,
                currentMaxRank
                );
            if (createResult.IsFailure)
            {
                _logger.LogError("Error creating project {ProjectName} for Portfolio {PortfolioId}. Error message: {Error}", request.Name, request.PortfolioId, createResult.Error);
                return Result.Failure<ProjectIdAndKey>(createResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            var project = createResult.Value;

            if (request.ProjectLifecycleId.HasValue)
            {
                var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                    .Include(l => l.Phases)
                    .FirstOrDefaultAsync(l => l.Id == request.ProjectLifecycleId.Value, cancellationToken);

                if (lifecycle is null)
                {
                    _logger.LogInformation("Project Lifecycle {LifecycleId} not found.", request.ProjectLifecycleId);
                    return Result.Failure<ProjectIdAndKey>("Project Lifecycle not found.");
                }

                var assignResult = project.AssignLifecycle(lifecycle);
                if (assignResult.IsFailure)
                {
                    _logger.LogWarning("Unable to assign lifecycle to project {ProjectId}. Error: {Error}", project.Id, assignResult.Error);
                    return Result.Failure<ProjectIdAndKey>(assignResult.Error);
                }

                await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Project {ProjectId} created with Key {ProjectKey}.", project.Id, project.Key.Value);

            return Result.Success(new ProjectIdAndKey(project.Id, project.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ProjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }

    private static Dictionary<ProjectRole, HashSet<Guid>> GetRoles(CreateProjectCommand request)
    {
        Dictionary<ProjectRole, HashSet<Guid>> roles = [];

        if (request.SponsorIds != null && request.SponsorIds.Count != 0)
        {
            roles.Add(ProjectRole.Sponsor, [.. request.SponsorIds]);
        }
        if (request.OwnerIds != null && request.OwnerIds.Count != 0)
        {
            roles.Add(ProjectRole.Owner, [.. request.OwnerIds]);
        }
        if (request.ManagerIds != null && request.ManagerIds.Count != 0)
        {
            roles.Add(ProjectRole.Manager, [.. request.ManagerIds]);
        }
        if (request.MemberIds != null && request.MemberIds.Count != 0)
        {
            roles.Add(ProjectRole.Member, [.. request.MemberIds]);
        }

        return roles;
    }
}
