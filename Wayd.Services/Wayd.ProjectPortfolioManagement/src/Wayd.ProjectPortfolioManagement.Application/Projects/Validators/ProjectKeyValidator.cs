using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Validators;

public sealed class ProjectKeyValidator : CustomValidator<ProjectKey>
{
    /// <summary>
    /// The failure for a key another project has. Also the answer when two requests claim one key at once: both
    /// pass this check, and the second meets the first at the unique index.
    /// </summary>
    public const string KeyTakenError = "The project key already exists.";

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext;
    private readonly Guid? _id;

    public ProjectKeyValidator(IProjectPortfolioManagementDbContext ppmDbContext, Guid? id = null)
    {
        _ppmDbContext = ppmDbContext;
        _id = id;

        RuleFor(x => x.Value)
            .MustAsync(BeUniqueProjectKey).OverridePropertyName(string.Empty).WithMessage(KeyTakenError);
    }

    private async Task<bool> BeUniqueProjectKey(string key, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.Projects.AsQueryable();

        if (_id.HasValue)
        {
            query = query.Where(p => p.Id != _id.Value);
        }

        return await query.AllAsync(x => x.Key != key, cancellationToken);
    }
}
