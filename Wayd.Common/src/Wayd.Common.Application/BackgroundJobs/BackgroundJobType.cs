using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Application.BackgroundJobs;

public enum BackgroundJobType
{
    // Integration Jobs

    [Display(Name = "People Full Sync", Description = "Run a full sync across all active people-sync connections (Entra, Workday). Re-fetches every worker and reconciles inactive employees.", Order = 1, GroupName = "Integration Jobs")]
    PeopleFullSync = 0,

    [Display(Name = "People Differential Sync", Description = "Run a differential sync across all active people-sync connections. Connectors that don't support incremental fetches transparently fall back to Full.", Order = 2, GroupName = "Integration Jobs")]
    PeopleDiffSync = 3,

    [Display(Name = "Work Full Sync", Description = "Run a full sync across all active work-management connections (Azure DevOps and any future Jira/GitHub connectors).", Order = 3, GroupName = "Integration Jobs")]
    WorkFullSync = 1,

    [Display(Name = "Work Differential Sync", Description = "Run a differential sync across all active work-management connections (only items changed since the last sync).", Order = 4, GroupName = "Integration Jobs")]
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


    // Maintenance Jobs

    [Display(Name = "Portfolio Rank Rebalance", Description = "Re-space project ranks within each portfolio to clean, gap-free whole numbers, removing fractional drift accumulated from drag-to-rank operations.", Order = 2001, GroupName = "Maintenance Jobs")]
    PortfolioRankRebalance = 2000,

    [Display(Name = "Import Stall Recovery", Description = "Reclaim imports no worker will finish: re-queue runs whose message never arrived, and fail runs that stopped reporting progress so their remaining rows can be resumed.", Order = 2002, GroupName = "Maintenance Jobs")]
    ImportStallRecovery = 2001,

    [Display(Name = "Import Retention Sweep", Description = "Delete the stored copy of every imported row belonging to a run that finished more than 30 days ago. Run history and per-row outcomes are kept.", Order = 2003, GroupName = "Maintenance Jobs")]
    ImportRetentionSweep = 2002,
}
