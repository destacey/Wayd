using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a strategic theme within the PPM domain.  The primary StrategicTheme entity is defined in the StrategicManagement domain.
/// </summary>
public sealed class StrategicTheme : IStrategicThemeData, IHasIdAndKey
{
    private StrategicTheme() { }

    public StrategicTheme(IStrategicThemeData strategicTheme)
    {
        Guard.Against.Null(strategicTheme, nameof(strategicTheme));

        Id = strategicTheme.Id;
        Key = strategicTheme.Key;
        Name = strategicTheme.Name;
        Description = strategicTheme.Description;
        State = strategicTheme.State;
    }

    public Guid Id { get; private init; }

    public int Key { get; private init; }

    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    public StrategicThemeState State { get; private set; }

    /// <summary>
    /// Update the strategic theme with the provided values.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="state"></param>
    public void Update(string name, string description, StrategicThemeState state)
    {
        Name = name;
        Description = description;
        State = state;
    }

    public void ChangeState(StrategicThemeState state)
    {
        State = state;
    }
}
