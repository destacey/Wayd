using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Extensions;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>Represents the status within a workflow.</summary>
/// <seealso cref="Wayd.Common.Domain.Data.BaseSoftDeletableEntity&lt;System.Guid&gt;" />
/// <seealso cref="Wayd.Common.Domain.Interfaces.IActivatable" />
public sealed class WorkStatus : BaseSoftDeletableEntity<int>, IActivatable
{
    private WorkStatus() { }

    private WorkStatus(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>The name of the work status.  The name cannot be changed.</summary>
    /// <value>The name.</value>
    public string Name
    {
        get;
        private init => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>The description of the work status.</summary>
    /// <value>The description.</value>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Indicates whether the work status is active or not.</summary>
    /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; private set; } = true;

    /// <summary>Updates the specified work status.</summary>
    /// <param name="description">The description.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public Result Update(string? description, Instant timestamp)
    {
        try
        {
            Description = description;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// The process for activating a work status.
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns>Result that indicates success or a list of errors</returns>
    public Result Activate(Instant timestamp)
    {
        if (!IsActive)
        {
            IsActive = true;
        }

        return Result.Success();
    }

    /// <summary>
    /// The process for deactivating a work status.
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

    /// <summary>Creates the work status.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static WorkStatus Create(string name, string? description, Instant timestamp)
    {
        WorkStatus status = new(name, description);
        return status;
    }
}
