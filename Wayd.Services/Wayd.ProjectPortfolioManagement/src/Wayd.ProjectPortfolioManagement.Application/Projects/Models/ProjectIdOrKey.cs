using System.Linq.Expressions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Interfaces;
using OneOf;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Models;

/// <summary>r
/// Represents either a Guid ID or a string project key.
/// </summary>
public sealed class ProjectIdOrKey : OneOfBase<Guid, ProjectKey>
{
    public ProjectIdOrKey(OneOf<Guid, ProjectKey> value) : base(value) { }

    /// <summary>Reads a route or query value as a Guid id or a project key.</summary>
    /// <exception cref="NotFoundException">The value is neither, so it can name no project.</exception>
    public ProjectIdOrKey(string value) : base(Parse(value)) { }

    private static OneOf<Guid, ProjectKey> Parse(string value)
    {
        if (Guid.TryParse(value, out var guid))
            return guid;

        try
        {
            return new ProjectKey(value);
        }
        catch (ArgumentException)
        {
            throw new NotFoundException($"No project has the id or key '{value}'.");
        }
    }

    public static implicit operator ProjectIdOrKey(Guid value) => new(OneOf<Guid, ProjectKey>.FromT0(value));
    public static implicit operator ProjectIdOrKey(ProjectKey value) => new(OneOf<Guid, ProjectKey>.FromT1(value));
    public static implicit operator ProjectIdOrKey(string value) => new(value);
}

public static class ProjectIdOrKeyExtensions
{
    /// <summary>
    /// Creates a filter expression that matches entities by project ID or key.
    /// </summary>
    public static Expression<Func<T, bool>> CreateFilter<T>(this ProjectIdOrKey idOrKey)
        where T : IHasIdAndKey<ProjectKey>
    {
        return idOrKey.Match<Expression<Func<T, bool>>>(
            id => x => x.Id == id,
            key => x => x.Key == key
        );
    }

    /// <summary>
    /// Creates a filter expression that matches entities by project ID or key.
    /// </summary>
    public static Expression<Func<T, bool>> CreateProjectFilter<T>(this ProjectIdOrKey idOrKey)
        where T : IHasProject
    {
        return idOrKey.Match<Expression<Func<T, bool>>>(
            id => x => x.ProjectId == id,
            key => x => x.Project.Key == key.Value
        );
    }
}
