using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.Common.Application.Attachments;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record UpdateProjectCommand(Guid Id, string Name, string Description, string? BusinessCase, string? ExpectedBenefits, int ExpenditureCategoryId, LocalDateRange? DateRange, List<Guid>? SponsorIds, List<Guid>? OwnerIds, List<Guid>? ManagerIds, List<Guid>? MemberIds, List<Guid>? StrategicThemeIds) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

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

        RuleFor(x => x.ExpenditureCategoryId)
            .GreaterThan(0);

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

public sealed class UpdateProjectCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<UpdateProjectCommandHandler> logger,
    IDateTimeProvider dateTimeProvider,
    IMarkdownAttachments markdownAttachments)
    : ICommandHandler<UpdateProjectCommand>
{
    private const string AppRequestName = nameof(UpdateProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProjectCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IMarkdownAttachments _markdownAttachments = markdownAttachments;

    public async Task<Result> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            // Portfolio and program roles are loaded because delivery leadership inherits downward — an
            // Owner/Manager on either may manage this project even without a role on the project itself.
            var project = await _projectPortfolioManagementDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure<ObjectIdAndKey>("Project not found.");
            }

            // Only a newly chosen category must be active: a project keeps a category archived after it
            // was assigned, and a whole-record update echoes that category back.
            if (project.ExpenditureCategoryId != request.ExpenditureCategoryId)
            {
                var expenditureCategoryState = await _projectPortfolioManagementDbContext.ExpenditureCategories
                    .Where(e => e.Id == request.ExpenditureCategoryId)
                    .Select(e => (ExpenditureCategoryState?)e.State)
                    .FirstOrDefaultAsync(cancellationToken);
                if (expenditureCategoryState is null)
                {
                    _logger.LogInformation("Expenditure Category with Id {ExpenditureCategoryId} not found.", request.ExpenditureCategoryId);
                    return Result.Failure("Expenditure Category not found.");
                }
                else if (expenditureCategoryState != ExpenditureCategoryState.Active)
                {
                    _logger.LogInformation("Expenditure Category with Id {ExpenditureCategoryId} is not active.", request.ExpenditureCategoryId);
                    return Result.Failure("Expenditure Category is not active.");
                }
            }

            var strategicThemes = request.StrategicThemeIds?.ToHashSet() ?? [];
            var themesAttachable = await _projectPortfolioManagementDbContext.EnsureThemesAttachable(
                strategicThemes,
                project.StrategicThemeTags.Select(t => t.StrategicThemeId).ToHashSet(),
                cancellationToken);
            if (themesAttachable.IsFailure)
            {
                _logger.LogInformation("Project {ProjectId} strategic themes refused: {Error}", request.Id, themesAttachable.Error);
                return Result.Failure(themesAttachable.Error);
            }

            var ancestry = project.AncestryRoles();

            // Copies only for someone the update will admit: copying reads every image the text borrows.
            IReadOnlyList<string?> markdown = [request.Description, request.BusinessCase, request.ExpectedBenefits];
            if (project.CanManageProject(actor, ancestry))
            {
                markdown = await _markdownAttachments.CopyForeign(PpmFollowOwners.Project.Key, project.Id, markdown, cancellationToken);
            }

            var updateResult = project.UpdateDetails(
                actor,
                ancestry,
                request.Name,
                markdown[0]!,
                markdown[1],
                markdown[2],
                request.ExpenditureCategoryId,
                _dateTimeProvider.Now
            );
            if (updateResult.IsFailure)
            {
                return HandleDomainFailure(project, updateResult);
            }

            var updateTimelineResult = project.UpdateTimeline(actor, ancestry, request.DateRange, _dateTimeProvider.Now);
            if (updateTimelineResult.IsFailure)
            {
                return HandleDomainFailure(project, updateTimelineResult);
            }

            var updateStrategicThemesResult = project.UpdateStrategicThemes(actor, ancestry, strategicThemes, _dateTimeProvider.Now);
            if (updateStrategicThemesResult.IsFailure)
            {
                return HandleDomainFailure(project, updateStrategicThemesResult);
            }

            // Last: roles decide who may manage the project, so an edit that removes the actor's own role
            // would otherwise refuse the rest of the actor's changes in the same request.
            var roles = GetRoles(request);
            var peopleExist = await _projectPortfolioManagementDbContext.EnsureEmployeesExist(roles.Values.SelectMany(ids => ids), cancellationToken);
            if (peopleExist.IsFailure)
                return Result.Failure(peopleExist.Error);
            var updateRolesResult = project.UpdateRoles(actor, ancestry, roles, _dateTimeProvider.Now);
            if (updateRolesResult.IsFailure)
            {
                return HandleDomainFailure(project, updateRolesResult);
            }

            await _markdownAttachments.Claim(
                PpmFollowOwners.Project.Key,
                project.Id,
                [project.Description, project.BusinessCase, project.ExpectedBenefits],
                cancellationToken);

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} updated.", request.Id);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ProjectSaves.StaleError);
        }
    }

    private static Dictionary<ProjectRole, HashSet<Guid>> GetRoles(UpdateProjectCommand request)
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

    private Result HandleDomainFailure(Project project, Result errorResult)
    {
        _logger.LogError("Unable to update Project {ProjectId}.  Error message: {Error}", project.Id, errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
