using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Enums.Imports;

/// <summary>
/// The kind of record an import row links to, which is what tells a client which page the link opens.
/// </summary>
/// <remarks>
/// Names the record the link opens, not necessarily the one the row created: a membership, a dependency, a
/// stage or a task has no page of its own, so its row links to the team, product or project it belongs to.
/// </remarks>
public enum ImportRecordType
{
    [Display(Name = "Employee", Order = 1)]
    Employee = 1,

    [Display(Name = "Team", Order = 2)]
    Team = 2,

    [Display(Name = "Team of Teams", Order = 3)]
    TeamOfTeams = 3,

    [Display(Name = "Planning Interval", Order = 4)]
    PlanningInterval = 4,

    [Display(Name = "Planning Interval Objective", Description = "Its link also carries the planning interval's key.", Order = 5)]
    PlanningIntervalObjective = 5,

    [Display(Name = "Risk", Order = 6)]
    Risk = 6,

    [Display(Name = "Portfolio", Order = 7)]
    Portfolio = 7,

    [Display(Name = "Program", Order = 8)]
    Program = 8,

    [Display(Name = "Project", Order = 9)]
    Project = 9,

    [Display(Name = "Strategic Initiative", Order = 10)]
    StrategicInitiative = 10,

    [Display(Name = "Strategic Theme", Order = 11)]
    StrategicTheme = 11,

    [Display(Name = "Product", Order = 12)]
    Product = 12,

    [Display(Name = "Version", Order = 13)]
    Version = 13,

    [Display(Name = "Release", Order = 14)]
    Release = 14,

    [Display(Name = "Release Package", Order = 15)]
    ReleasePackage = 15,

    [Display(Name = "Deployment", Order = 16)]
    Deployment = 16,

    [Display(Name = "Deployment Environment", Description = "Has no page of its own; listed with the other environments.", Order = 17)]
    DeploymentEnvironment = 17,

    [Display(Name = "Intake Request", Order = 18)]
    IntakeRequest = 18,
}
