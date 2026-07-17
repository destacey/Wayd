using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// Represents a type of work item.
/// </summary>
/// <seealso cref="Wayd.Common.Domain.Data.BaseSoftDeletableEntity&lt;System.Guid&gt;" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.IActivatable" />
public sealed class WorkType : BaseSoftDeletableEntity<int>, IActivatable
{
    private WorkType() { }

    private WorkType(string name, string? description, int levelId)
    {
        Name = name;
        Description = description;
        LevelId = levelId;
    }

    /// <summary>The name of the work type.  The name cannot be changed.</summary>
    /// <value>The name.</value>
    public string Name
    {
        get;
        init => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>The description of the work type.</summary>
    /// <value>The description.</value>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    public int LevelId { get; private set; }

    public WorkTypeLevel? Level { get; set; }

    /// <summary>Indicates whether the work type is active or not.</summary>
    /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; private set; } = true;

    /// <summary>Updates the specified work type.</summary>
    /// <param name="description">The description.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public Result Update(string? description, int levelId, Instant timestamp)
    {
        try
        {
            Description = description;
            LevelId = levelId;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// The process for activating a work type.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            // TODO is there logic that would prevent activation?
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a work type.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Deactivate(Instant timestamp)
    {
        if (IsActive)
        {
            IsActive = false;
        }

        return Result.Success();
    }

    /// <summary>Creates the a work type.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="levelId">The level identifier.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static WorkType Create(string name, string? description, int levelId, Instant timestamp)
    {
        WorkType workType = new(name, description, levelId);
        return workType;
    }
}
