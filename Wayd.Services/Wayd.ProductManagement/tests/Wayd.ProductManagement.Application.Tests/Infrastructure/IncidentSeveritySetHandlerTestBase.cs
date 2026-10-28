using Moq;
using Wayd.ProductManagement.Application.IncidentSeveritySets;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Infrastructure;

/// <summary>
/// Shared seeding and mocks for incident severity set handlers.
/// </summary>
public abstract class IncidentSeveritySetHandlerTestBase : ProductCommandTestBase
{
    protected readonly Mock<IIncidentSeverityLevelUsageQuery> LevelUsage = new();

    protected IncidentSeveritySetHandlerTestBase()
    {
        LevelUsage
            .Setup(q => q.GetLevelIdsInUse(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
    }

    protected IncidentSeveritySet SeedCustomSeveritySet(
        string name = "Custom",
        IReadOnlyList<string>? levelNames = null)
    {
        var set = IncidentSeveritySet.CreateCustom(name, null, levelNames ?? ["High", "Low"]).Value;
        DbContext.AddIncidentSeveritySet(set);

        return set;
    }

    protected IncidentSeveritySet SeedSystemSeveritySet(
        string name = "SEV",
        IReadOnlyList<string>? levelNames = null)
    {
        var set = IncidentSeveritySet.CreateSystem(name, null, levelNames ?? ["SEV-1", "SEV-2"]).Value;
        DbContext.AddIncidentSeveritySet(set);

        return set;
    }

    protected void SeedOrganizationSelection(IncidentSeveritySet selected) =>
        DbContext.AddIncidentSeveritySetAssignment(IncidentSeveritySetAssignment.Create(selected.Id));
}
