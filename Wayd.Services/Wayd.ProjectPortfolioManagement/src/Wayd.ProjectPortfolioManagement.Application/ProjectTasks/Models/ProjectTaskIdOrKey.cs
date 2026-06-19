using System.Linq.Expressions;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using OneOf;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Models;

/// <summary>
/// Represents either a Guid ID or a TaskKey (string like "APOLLO-T001").
/// </summary>
public sealed class ProjectTaskIdOrKey : OneOfBase<Guid, ProjectTaskKey>
{
    public ProjectTaskIdOrKey(OneOf<Guid, ProjectTaskKey> value) : base(value) { }

    public ProjectTaskIdOrKey(string value) : base(Guid.TryParse(value, out var guid) ? guid : new ProjectTaskKey(value)) { }

    /// <summary>
    /// Implicitly converts a Guid to an <see cref="ProjectTaskIdOrKey"/>.
    /// </summary>
    /// <param name="value">The Guid value to convert.</param>
    public static implicit operator ProjectTaskIdOrKey(Guid value) => new(OneOf<Guid, ProjectTaskKey>.FromT0(value));

    /// <summary>
    /// Converts a <see cref="ProjectTaskKey"/> to a <see cref="ProjectTaskIdOrKey"/> instance.
    /// </summary>
    /// <remarks>This implicit conversion allows a <see cref="ProjectTaskKey"/> to be used wherever a <see
    /// cref="ProjectTaskIdOrKey"/> is expected, enabling seamless assignment and method calls without explicit
    /// casting.</remarks>
    /// <param name="value">The project task key to convert.</param>
    public static implicit operator ProjectTaskIdOrKey(ProjectTaskKey value) => new(OneOf<Guid, ProjectTaskKey>.FromT1(value));

    /// <summary>
    /// Converts a string containing a project task ID or key to a ProjectTaskIdOrKey instance.
    /// </summary>
    /// <param name="value">The string representation of the project task ID or key to convert. Cannot be null.</param>
    public static implicit operator ProjectTaskIdOrKey(string value) => new(value);
}

public static class TaskIdOrKeyExtensions
{
    /// <summary>
    /// Creates a filter expression that matches entities by project ID or key.
    /// </summary>
    public static Expression<Func<T, bool>> CreateFilter<T>(this ProjectTaskIdOrKey idOrKey)
        where T : IHasIdAndKey<ProjectTaskKey>
    {
        return idOrKey.Match<Expression<Func<T, bool>>>(
            id => x => x.Id == id,
            key => x => x.Key == key
        );
    }
}
