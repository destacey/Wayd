using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

public class PpmGeneratorTests
{
    private static GeneratedPpm Generate(PpmOptions? ppmOptions = null, OrgOptions? orgOptions = null)
    {
        var org = new OrgGenerator(orgOptions ?? new OrgOptions { ValueStreams = 3, Teams = 15, Seed = 1234 }).Generate();
        return new PpmGenerator(org.Structure, ppmOptions ?? new PpmOptions { Seed = 5678 }).Generate();
    }

    private static IEnumerable<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(';');

    [Fact]
    public void Generate_EveryProgramReferencesAnExistingPortfolio()
    {
        // Arrange
        var ppm = Generate();
        var portfolioNames = ppm.Portfolios.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.Programs.Where(p => !portfolioNames.Contains(p.PortfolioName)).ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryProjectReferencesAnExistingPortfolio()
    {
        // Arrange
        var ppm = Generate();
        var portfolioNames = ppm.Portfolios.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.Projects.Where(p => !portfolioNames.Contains(p.PortfolioName)).ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryProjectProgramReferenceResolves()
    {
        // Arrange
        var ppm = Generate();
        var programNames = ppm.Programs.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.Projects
            .Where(p => p.ProgramName is not null && !programNames.Contains(p.ProgramName))
            .ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EachPortfolioHasSeveralThematicPrograms()
    {
        // Arrange — programs are the portfolio's thematic groupings, so a portfolio runs a handful, not one.
        var ppm = Generate();
        // Value-stream portfolios are the ones with programs; function portfolios are portfolio-direct. The
        // value-stream description ends "… value stream." (singular), while function ones say "value streams".
        var valueStreamPortfolios = ppm.Portfolios
            .Where(p => p.Description.EndsWith("value stream."))
            .Select(p => p.Name)
            .ToList();

        // Act
        var programCounts = valueStreamPortfolios
            .ToDictionary(name => name, name => ppm.Programs.Count(pr => string.Equals(pr.PortfolioName, name, StringComparison.OrdinalIgnoreCase)));

        // Assert — every value-stream portfolio has more than one program.
        programCounts.Values.Should().OnlyContain(c => c > 1);
    }

    [Fact]
    public void Generate_ProgramNamesAreThematic()
    {
        // Arrange — program names are built from a theme (Modernization, Integrations, …), not an ART.
        var ppm = Generate();
        var themeWords = PpmVocabulary.ProgramThemes.Select(t => t.Name);

        // Act / Assert — every program name contains one of the theme names.
        ppm.Programs.Should().OnlyContain(p => themeWords.Any(t => p.Name.Contains(t)));
    }

    [Fact]
    public void Generate_AssignedProgramWindowCoversTheProject()
    {
        // Arrange — a project can only belong to a program whose own window contains the project's window.
        var ppm = Generate();
        var programsByName = ppm.Programs.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // Act
        var violations = ppm.Projects
            .Where(p => p.ProgramName is not null)
            .Where(p =>
            {
                var program = programsByName[p.ProgramName!];
                return program.Start > p.Start || program.End < p.End;
            })
            .ToList();

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void Generate_SomeProjectsArePortfolioDirect()
    {
        // Arrange — a minority of projects are standalone (no program), which is realistic.
        var ppm = Generate();

        // Act
        var portfolioDirect = ppm.Projects.Count(p => p.ProgramName is null);

        // Assert — present, but the clear minority.
        portfolioDirect.Should().BeGreaterThan(0);
        ((double)portfolioDirect / ppm.Projects.Count).Should().BeLessThan(0.5);
    }

    [Fact]
    public void Generate_ProgramCountScalesWithConcurrency()
    {
        // Arrange — the knob is concurrency and the total is derived, so more concurrent programs yields more.
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 2, Teams = 12, Seed = 4242 }).Generate();

        // Act
        var few = new PpmGenerator(org.Structure, new PpmOptions { ConcurrentProgramsPerPortfolio = 2, Seed = 7 }).Generate();
        var many = new PpmGenerator(org.Structure, new PpmOptions { ConcurrentProgramsPerPortfolio = 5, Seed = 7 }).Generate();

        // Assert
        many.Programs.Count.Should().BeGreaterThan(few.Programs.Count);
    }

    [Fact]
    public void Generate_ProjectKeysAreUnique()
    {
        // Arrange
        var ppm = Generate();

        // Act / Assert
        ppm.Projects.Select(p => p.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_ProjectKeysAreValidFormat()
    {
        // Arrange — project keys are 2-20 uppercase alphanumeric characters.
        var ppm = Generate();

        // Act / Assert
        ppm.Projects.Select(p => p.Key).Should().OnlyContain(k => System.Text.RegularExpressions.Regex.IsMatch(k, "^[A-Z0-9]{2,20}$"));
    }

    [Fact]
    public void Generate_EveryThemeReferenceOnProgramsAndProjectsResolves()
    {
        // Arrange
        var ppm = Generate();
        var themeNames = ppm.StrategicThemes.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var badRefs = ppm.Programs.SelectMany(p => Split(p.StrategicThemes))
            .Concat(ppm.Projects.SelectMany(p => Split(p.StrategicThemes)))
            .Where(t => !themeNames.Contains(t))
            .ToList();

        // Assert
        badRefs.Should().BeEmpty();
    }

    [Fact]
    public void Generate_AllStrategicThemesAreActive()
    {
        // Arrange — only active themes can be attached to programs and projects.
        var ppm = Generate();

        // Act / Assert
        ppm.StrategicThemes.Should().OnlyContain(t => t.State == "Active");
    }

    [Fact]
    public void Generate_EveryTaskReferencesAnExistingProject()
    {
        // Arrange
        var ppm = Generate();
        var projectKeys = ppm.Projects.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.ProjectTasks.Where(t => !projectKeys.Contains(t.ProjectKey)).ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryTaskPhaseIsOnTheLifecycle()
    {
        // Arrange
        var ppm = Generate();
        var phaseNames = ppm.Lifecycle.Phases.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var badPhases = ppm.ProjectTasks.Where(t => !phaseNames.Contains(t.PhaseName)).ToList();

        // Assert
        badPhases.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryTaskParentIsNamedWithinItsProject()
    {
        // Arrange — a child task's parent must be another task in the same project.
        var ppm = Generate();
        var tasksByProject = ppm.ProjectTasks
            .GroupBy(t => t.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.ProjectTasks
            .Where(t => t.ParentTaskName is not null && !tasksByProject[t.ProjectKey].Contains(t.ParentTaskName))
            .ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_TaskNamesAreUniqueWithinAProject()
    {
        // Arrange — names are how child rows reference parents, so they must be unambiguous per project.
        var ppm = Generate();

        // Act
        var duplicated = ppm.ProjectTasks
            .GroupBy(t => t.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Where(n => n.Count() > 1))
            .ToList();

        // Assert
        duplicated.Should().BeEmpty();
    }

    [Fact]
    public void Generate_MilestonesHaveADateAndNoRangeOrProgress()
    {
        // Arrange
        var ppm = Generate();

        // Act
        var milestones = ppm.ProjectTasks.Where(t => t.Type == "Milestone").ToList();

        // Assert
        milestones.Should().OnlyContain(m => m.PlannedDate != null && m.PlannedStart == null && m.PlannedEnd == null && m.Progress == null);
    }

    [Fact]
    public void Generate_TasksHaveProgressAndARangeButNoSingleDate()
    {
        // Arrange
        var ppm = Generate();

        // Act
        var tasks = ppm.ProjectTasks.Where(t => t.Type == "Task").ToList();

        // Assert
        tasks.Should().OnlyContain(t => t.Progress != null && t.PlannedDate == null);
    }

    [Fact]
    public void Generate_ProposedAndApprovedProjectsHaveNoTasks()
    {
        // Arrange — a project needs to be past the proposal stage before it has a work breakdown.
        var ppm = Generate();
        var projectsWithTasks = ppm.ProjectTasks.Select(t => t.ProjectKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var proposedWithTasks = ppm.Projects
            .Where(p => (p.Status == "Proposed" || p.Status == "Approved") && projectsWithTasks.Contains(p.Key))
            .ToList();

        // Assert
        proposedWithTasks.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EmitsAPhaseRowForEveryPhaseOfATaskedProject()
    {
        // Arrange — the phase-status import is separate data, so a project with tasks needs a phase row per
        // lifecycle phase.
        var ppm = Generate();
        var phaseNames = ppm.Lifecycle.Phases.Select(p => p.Name).ToList();
        var taskedProjects = ppm.ProjectTasks.Select(t => t.ProjectKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act / Assert — every project that has tasks has a phase row for each lifecycle phase.
        foreach (var key in taskedProjects)
        {
            var phases = ppm.ProjectPhases.Where(p => string.Equals(p.ProjectKey, key, StringComparison.OrdinalIgnoreCase)).Select(p => p.PhaseName);
            phases.Should().BeEquivalentTo(phaseNames);
        }
    }

    [Fact]
    public void Generate_PhaseStatusMatchesTheRollupOfItsTasks()
    {
        // Arrange — the generator owns the rollup rule (the import applies phase status verbatim), so each
        // emitted phase status must equal the rollup of that phase's own tasks.
        var ppm = Generate();

        static string RollUp(IReadOnlyCollection<string> statuses)
        {
            if (statuses.Count == 0) return "NotStarted";
            bool Closed(string s) => s is "Completed" or "Cancelled";
            if (statuses.All(Closed) && statuses.Any(s => s == "Completed")) return "Completed";
            if (statuses.All(s => s == "NotStarted")) return "NotStarted";
            return "InProgress";
        }

        var tasksByPhase = ppm.ProjectTasks
            .GroupBy(t => (t.ProjectKey, t.PhaseName))
            .ToDictionary(g => g.Key, g => g.Select(t => t.Status).ToList());

        // Act
        var mismatches = ppm.ProjectPhases
            .Where(ph => tasksByPhase.TryGetValue((ph.ProjectKey, ph.PhaseName), out var statuses)
                ? ph.Status != RollUp(statuses)
                : ph.Status != "NotStarted")
            .ToList();

        // Assert
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryPhaseRowReferencesAnExistingProject()
    {
        // Arrange
        var ppm = Generate();
        var projectKeys = ppm.Projects.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.ProjectPhases.Where(p => !projectKeys.Contains(p.ProjectKey)).ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryInitiativeProjectReferenceResolves()
    {
        // Arrange
        var ppm = Generate();
        var projectKeys = ppm.Projects.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var badRefs = ppm.StrategicInitiatives
            .SelectMany(i => Split(i.ProjectKeys))
            .Where(k => !projectKeys.Contains(k))
            .ToList();

        // Assert
        badRefs.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryKpiReferencesAnExistingInitiative()
    {
        // Arrange
        var ppm = Generate();
        var initiativeNames = ppm.StrategicInitiatives.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var dangling = ppm.StrategicInitiativeKpis
            .Where(k => !initiativeNames.Contains(k.StrategicInitiativeName))
            .ToList();

        // Assert
        dangling.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EveryFinalizedProgramExistsAndBelongsToItsPortfolio()
    {
        // Arrange
        var ppm = Generate();
        var programsByPortfolio = ppm.Programs
            .Select(p => (p.Name, p.PortfolioName))
            .ToHashSet();

        // Act
        var badFinalizations = ppm.Finalizations
            .Where(f => f.Type == "Program" && !programsByPortfolio.Contains((f.Name, f.PortfolioName!)))
            .ToList();

        // Assert
        badFinalizations.Should().BeEmpty();
    }

    [Fact]
    public void Generate_ProducesAPortfolioPerValueStreamPlusTheFunctionPortfolios()
    {
        // Arrange
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 15, Seed = 1234 }).Generate();

        // Act
        var ppm = new PpmGenerator(org.Structure, new PpmOptions { FunctionPortfolios = 2, Seed = 5678 }).Generate();

        // Assert — one portfolio per value stream, plus the requested function portfolios.
        ppm.Portfolios.Should().HaveCount(org.Structure.ValueStreams.Count + 2);
        ppm.Portfolios.Select(p => p.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_RoleAssignmentsReferenceGeneratedEmployeeNumbers()
    {
        // Arrange — every person referenced on a portfolio/program/project must be a generated employee.
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 15, Seed = 1234 }).Generate();
        var ppm = new PpmGenerator(org.Structure, new PpmOptions { Seed = 5678 }).Generate();
        var employeeNumbers = org.Employees.Select(e => e.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var referenced = ppm.Portfolios.SelectMany(p => Split(p.Sponsors).Concat(Split(p.Owners)).Concat(Split(p.Managers)))
            .Concat(ppm.Programs.SelectMany(p => Split(p.Sponsors).Concat(Split(p.Owners)).Concat(Split(p.Managers))))
            .Concat(ppm.Projects.SelectMany(p => Split(p.Sponsors).Concat(Split(p.Owners)).Concat(Split(p.Managers)).Concat(Split(p.Members))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert
        referenced.Should().OnlyContain(n => employeeNumbers.Contains(n));
    }

    [Fact]
    public void Generate_MostProjectsAreMultiTeam()
    {
        // Arrange — projects are ART-scoped: a subset of the ART's teams collaborate, so single-team is the
        // minority. A team is a handful of people, so a project staffed from one team has far fewer members
        // than a multi-team one. Using a large employee base makes the member counts separable.
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 24, Seed = 20240721 }).Generate();
        var ppm = new PpmGenerator(org.Structure, new PpmOptions { Seed = 99 }).Generate();

        // The smallest possible team has 5 members (3 ICs + EM + PO), so >9 members means two or more teams.
        var multiTeam = ppm.Projects.Count(p => Split(p.Members).Count() > 9);

        // Act
        var multiTeamShare = (double)multiTeam / ppm.Projects.Count;

        // Assert — the majority of projects span more than one team.
        multiTeamShare.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void Generate_ProjectMembersAreDistinctEmployeeNumbers()
    {
        // Arrange — a cross-team project's members are the union across its teams, with no duplicates even
        // when someone would otherwise appear via two teams.
        var ppm = Generate();

        // Act / Assert
        foreach (var project in ppm.Projects)
        {
            var members = Split(project.Members).ToList();
            members.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void Generate_ScalesProjectCountWithArtConcurrency()
    {
        // Arrange — the knob is concurrency, and the total is derived from it, so doubling the concurrent
        // load should roughly double the number of projects generated.
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 2, Teams = 12, Seed = 4242 }).Generate();

        // Act
        var low = new PpmGenerator(org.Structure, new PpmOptions { ConcurrentProjectsPerArt = 5, Seed = 7 }).Generate();
        var high = new PpmGenerator(org.Structure, new PpmOptions { ConcurrentProjectsPerArt = 10, Seed = 7 }).Generate();

        // Assert — more concurrency yields materially more projects (not a strict 2x, but clearly higher).
        high.Projects.Count.Should().BeGreaterThan(low.Projects.Count);
    }

    [Fact]
    public void Generate_SomeProjectsSpanMultipleArts()
    {
        // Arrange — a minority of projects reach across ARTs within a value stream. Map each member's
        // employee number to the ART its team belongs to, then look for a project whose members come from
        // more than one ART. A large org and project count make the ~10% cross-ART slice reliably present.
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 2, Teams = 24, Seed = 20240721 }).Generate();
        var ppm = new PpmGenerator(org.Structure, new PpmOptions { Seed = 314 }).Generate();

        // employee number → ART team code (the ART the member's team sits under)
        var artByEmployee = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vs in org.Structure.ValueStreams)
            foreach (var art in vs.Arts)
                foreach (var team in art.Teams)
                    foreach (var emp in team.MemberEmployeeNumbers)
                        artByEmployee[emp] = art.TeamCode;

        // Act — projects whose members map to more than one ART.
        var crossArt = ppm.Projects.Count(p =>
            Split(p.Members)
                .Where(artByEmployee.ContainsKey)
                .Select(m => artByEmployee[m])
                .Distinct()
                .Count() > 1);

        // Assert — cross-ART projects exist, but stay a minority.
        crossArt.Should().BeGreaterThan(0);
        ((double)crossArt / ppm.Projects.Count).Should().BeLessThan(0.3);
    }

    [Fact]
    public void Generate_ProducesAHistoryAndARunway()
    {
        // Arrange — with a rich per-ART project count the window should hold finished work, in-flight work,
        // and not-yet-started work at once.
        var ppm = Generate();

        // Act
        var statuses = ppm.Projects.Select(p => p.Status).ToHashSet();

        // Assert
        statuses.Should().Contain("Completed");
        statuses.Should().Contain("Active");
        (statuses.Contains("Proposed") || statuses.Contains("Approved")).Should().BeTrue();
    }

    [Fact]
    public void Generate_IsReproducibleForAFixedSeed()
    {
        // Arrange / Act — the same seed reproduces the same generated project keys.
        var first = Generate();
        var second = Generate();

        // Assert
        second.Projects.Select(p => p.Key).Should().Equal(first.Projects.Select(p => p.Key));
        second.Portfolios.Select(p => p.Name).Should().Equal(first.Portfolios.Select(p => p.Name));
    }
}
