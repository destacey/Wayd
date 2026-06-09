using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;

public static class StrategicInitiativeDataExtensions
{
    public static StrategicInitiative AddKpis(this StrategicInitiative strategicInitiative, int count)
    {
        if (count <= 0) return strategicInitiative;

        var kpiFaker = new StrategicInitiativeKpiFaker();

        var kpisList = GenericExtensions.GetPrivateHashSet<StrategicInitiativeKpi>(strategicInitiative, "_kpis");

        for (int i = 0; i < count; i++)
        {
            var kpi = kpiFaker.WithStrategicInitiativeId(strategicInitiative.Id).WithOrder(i + 1).Generate();
            kpisList.Add(kpi);
        }

        return strategicInitiative;
    }

    public static StrategicInitiative AddProjects(this StrategicInitiative strategicInitiative, int count, TestingDateTimeProvider dateTimeProvider)
    {
        if (count <= 0) return strategicInitiative;

        var projectFaker = new ProjectFaker();

        var projectsList = GenericExtensions.GetPrivateHashSet<StrategicInitiativeProject>(strategicInitiative, "_strategicInitiativeProjects");

        // TODO: add logic based on the current status of the strategic initiative
        for (int i = 0; i < count - 1; i++)
        {
            var project = projectFaker.AsActive(dateTimeProvider, strategicInitiative.PortfolioId);
            projectsList.Add(CreateStrategicInitiativeProject(strategicInitiative.Id, project));
        }

        // Add a proposed strategic initiative as the last one
        var lastProject = projectFaker.AsProposed(dateTimeProvider, strategicInitiative.PortfolioId);
        projectsList.Add(CreateStrategicInitiativeProject(strategicInitiative.Id, lastProject));

        return strategicInitiative;
    }

    private static StrategicInitiativeProject CreateStrategicInitiativeProject(Guid strategicInitiativeId, Project project)
    {
        var strategicInitiativeProject = StrategicInitiativeProject.Create(strategicInitiativeId, project.Id);

        strategicInitiativeProject.SetPrivate(p => p.Project, project);

        return strategicInitiativeProject;
    }
}