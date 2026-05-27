using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Application.BackgroundJobs;

public enum BackgroundJobType
{
    // Integration Jobs

    [Display(Name = "People Sync", Description = "Synchronize people (employees and contingent workers) from all active people-sync connections (Entra, future Workday).", Order = 1, GroupName = "Integration Jobs")]
    PeopleSync = 0,

    [Display(Name = "Work Full Sync", Description = "Run a full sync across all active work-management connections (Azure DevOps and any future Jira/GitHub connectors).", Order = 2, GroupName = "Integration Jobs")]
    WorkFullSync = 1,

    [Display(Name = "Work Differential Sync", Description = "Run a differential sync across all active work-management connections (only items changed since the last sync).", Order = 3, GroupName = "Integration Jobs")]
    WorkDiffSync = 2,


    // Data Replication Jobs

    [Display(Name = "Team Graph Sync", Description = "Synchronize the latest team data into the Graph tables.", Order = 1004, GroupName = "Data Replication Jobs")]
    TeamGraphSync = 1000,

    [Display(Name = "Strategic Themes Sync", Description = "Synchronize the latest strategic themes data.", Order = 1003, GroupName = "Data Replication Jobs")]
    StrategicThemesSync = 1001,

    [Display(Name = "Projects Sync", Description = "Synchronize the latest projects data.", Order = 1002, GroupName = "Data Replication Jobs")]
    ProjectsSync = 1002,

    [Display(Name = "Iterations Sync", Description = "Synchronize the latest iterations data.", Order = 1001, GroupName = "Data Replication Jobs")]
    IterationsSync = 1003,

    [Display(Name = "Teams Sync", Description = "Synchronize the latest teams data.", Order = 1005, GroupName = "Data Replication Jobs")]
    TeamsSync = 1004,
}
