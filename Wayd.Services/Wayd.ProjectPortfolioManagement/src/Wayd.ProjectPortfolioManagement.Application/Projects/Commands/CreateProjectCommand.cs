using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Validators;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

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
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IMarkdownAttachments markdownAttachments)
    : ICommandHandler<CreateProjectCommand, ProjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateProjectCommand);

    /// <summary>The failure when an image in the text changed while the project was being saved.</summary>
    public const string ImageConflictError = "An image in the text changed while the project was being saved. Save again.";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateProjectCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IMarkdownAttachments _markdownAttachments = markdownAttachments;

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
            var themesAttachable = await _projectPortfolioManagementDbContext.EnsureThemesAttachable(strategicThemeIds, alreadyAttached: null, cancellationToken);
            if (themesAttachable.IsFailure)
            {
                _logger.LogInformation("Strategic themes refused for a new project: {Error}", themesAttachable.Error);
                return Result.Failure<ProjectIdAndKey>(themesAttachable.Error);
            }

            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                    .Include(p => p.Programs)
                    .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", request.PortfolioId);
                return Result.Failure<ProjectIdAndKey>("Portfolio not found.");
            }

            ProjectLifecycle? lifecycle = null;
            if (request.ProjectLifecycleId.HasValue)
            {
                lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                    .Include(l => l.Stages)
                    .FirstOrDefaultAsync(l => l.Id == request.ProjectLifecycleId.Value, cancellationToken);
                if (lifecycle is null)
                {
                    _logger.LogInformation("Project Lifecycle {LifecycleId} not found.", request.ProjectLifecycleId);
                    return Result.Failure<ProjectIdAndKey>("Project Lifecycle not found.");
                }

                // Checked again by AssignLifecycle, but refused here as well: CopyForeign below copies
                // image files in storage immediately, and a later refusal would leave them orphaned.
                if (lifecycle.State != ProjectLifecycleState.Active)
                {
                    _logger.LogInformation("Project Lifecycle {LifecycleId} is not active.", request.ProjectLifecycleId);
                    return Result.Failure<ProjectIdAndKey>("Only active lifecycles can be assigned to projects.");
                }
            }

            var roles = GetRoles(request);
            var peopleExist = await _projectPortfolioManagementDbContext.EnsureEmployeesExist(roles.Values.SelectMany(ids => ids), cancellationToken);
            if (peopleExist.IsFailure)
                return Result.Failure<ProjectIdAndKey>(peopleExist.Error);

            var projectKey = new ProjectKey(request.Key);

            // Cheap scalar so the new project can be ranked at the bottom without loading every
            // sibling project into the aggregate.
            var currentMaxRank = await _projectPortfolioManagementDbContext.Projects
                .Where(p => p.PortfolioId == request.PortfolioId)
                .MaxAsync(p => (double?)p.Rank, cancellationToken);

            var markdown = await _markdownAttachments.CopyForeign(
                PpmFollowOwners.Project.Key,
                null,
                [request.Description, request.BusinessCase, request.ExpectedBenefits],
                cancellationToken);

            var createResult = portfolio.CreateProject(
                request.Name,
                markdown[0]!,
                projectKey,
                request.ExpenditureCategoryId,
                request.DateRange,
                request.ProgramId,
                markdown[1],
                markdown[2],
                roles,
                strategicThemeIds,
                _dateTimeProvider.Now,
                _currentUser.AttributionOnlyActor(),
                currentMaxRank
                );
            if (createResult.IsFailure)
            {
                _logger.LogError("Error creating project {ProjectName} for Portfolio {PortfolioId}. Error message: {Error}", request.Name, request.PortfolioId, createResult.Error);
                return Result.Failure<ProjectIdAndKey>(createResult.Error);
            }

            var project = createResult.Value;

            // Assigned before the save, so a refused assignment leaves no project without its lifecycle.
            if (lifecycle is not null)
            {
                // PpmActor.System: this runs as part of creating the project, so nobody holds a role on it
                // yet. Authorization for creation is the caller's Permissions.Projects.Create claim.
                var assignResult = project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, _dateTimeProvider.Now);
                if (assignResult.IsFailure)
                {
                    _logger.LogInformation("Unable to assign lifecycle {LifecycleId} to new project {ProjectKey}. Error: {Error}", lifecycle.Id, project.Key.Value, assignResult.Error);
                    return Result.Failure<ProjectIdAndKey>(assignResult.Error);
                }
            }

            await _markdownAttachments.Claim(
                PpmFollowOwners.Project.Key,
                project.Id,
                [project.Description, project.BusinessCase, project.ExpectedBenefits],
                cancellationToken);

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} created with Key {ProjectKey}.", project.Id, project.Key.Value);

            return Result.Success(new ProjectIdAndKey(project.Id, project.Key));
        }
        catch (DbUpdateException ex) when (DbUpdateErrors.IsUniqueViolation(ex))
        {
            throw new ConflictException(ProjectKeyValidator.KeyTakenError);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Only an image can conflict on a new project: another save claimed it, or the sweep deleted it.
            return Result.Failure<ProjectIdAndKey>(ImageConflictError);
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
