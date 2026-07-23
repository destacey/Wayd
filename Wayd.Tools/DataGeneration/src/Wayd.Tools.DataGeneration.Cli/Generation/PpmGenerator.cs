using Bogus;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates a coherent PPM dataset on top of a generated <see cref="OrgStructure"/>. Each value stream
/// becomes a portfolio, each ART a program, and each leaf team a handful of projects staffed from that
/// team's own people; a couple of cross-cutting business-function portfolios pull projects from teams across
/// value streams. Strategic themes sit above and are tagged onto programs and projects; strategic
/// initiatives with KPIs give each portfolio a longer-horizon outcome.
/// <para>
/// Work is spread across a four-year window — two years of history and a two-year runway — and each item's
/// status follows its position on that window: finished in the past, in flight around today, proposed in the
/// future. Because the domain only lets a program or portfolio close once its contents are closed, the
/// historical ones are emitted active and closed by a separate set of finalize rows (see the finalize import).
/// </para>
/// </summary>
public sealed class PpmGenerator
{
    private readonly OrgStructure _org;
    private readonly PpmOptions _options;
    private readonly Faker _faker;

    private readonly List<StrategicThemeCsvRow> _themes = [];
    private readonly List<PortfolioCsvRow> _portfolios = [];
    private readonly List<ProgramCsvRow> _programs = [];
    private readonly List<ProjectCsvRow> _projects = [];
    private readonly List<ProjectTaskCsvRow> _tasks = [];
    private readonly List<ProjectPhaseCsvRow> _phaseStatuses = [];
    private readonly List<StrategicInitiativeCsvRow> _initiatives = [];
    private readonly List<StrategicInitiativeKpiCsvRow> _kpis = [];
    private readonly List<PpmFinalizationCsvRow> _finalizations = [];

    private readonly HashSet<string> _usedProjectKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedInitiativeNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _themeNames = [];

    /// <summary>A generated program with the theme and window a project is matched against.</summary>
    private sealed record GeneratedProgram(string Name, PpmVocabulary.ProgramTheme Theme, DateTime Start, DateTime End, string Status);

    // Today anchors the four-year window: two years back, two years forward.
    private static readonly DateTime Today = DateTime.UtcNow.Date;
    private static readonly DateTime WindowStart = Today.AddYears(-HistoryYears);
    private static readonly DateTime WindowEnd = Today.AddYears(RunwayYears);
    private const int HistoryYears = 2;
    private const int RunwayYears = 2;

    // A project runs 2-9 months typically. The average feeds the concurrency-to-total derivation.
    private const int MinProjectMonths = 2;
    private const int MaxProjectMonths = 9;

    // A thematic program runs 1-3 years. The average feeds its own concurrency-to-total derivation.
    private const int MinProgramMonths = 12;
    private const int MaxProgramMonths = 36;

    // Roughly one project in ten reaches across ARTs within its value stream.
    private const double CrossArtFraction = 0.10;

    // About one project in six is standalone (portfolio-direct) rather than grouped under a program.
    private const double PortfolioDirectFraction = 0.15;

    public PpmGenerator(OrgStructure org, PpmOptions options)
    {
        _org = org;
        _options = options;
        _faker = new Faker { Random = new Randomizer(options.Seed) };
    }

    public GeneratedPpm Generate()
    {
        BuildThemes();
        BuildValueStreamPortfolios();
        BuildFunctionPortfolios();

        return new GeneratedPpm(
            _themes,
            _portfolios,
            _programs,
            _projects,
            _tasks,
            _phaseStatuses,
            _initiatives,
            _kpis,
            _finalizations,
            PpmVocabulary.ExpenditureCategories,
            PpmVocabulary.StandardLifecycle);
    }

    // ---- Themes -------------------------------------------------------------------------------

    private void BuildThemes()
    {
        // A handful of active themes to tag work with. All active so programs and projects can attach them.
        var names = _faker.PickRandom(PpmVocabulary.StrategicThemes, Math.Min(6, PpmVocabulary.StrategicThemes.Length)).ToList();
        foreach (var name in names)
        {
            _themeNames.Add(name);
            _themes.Add(new StrategicThemeCsvRow
            {
                Name = name,
                Description = $"{name}: a cross-cutting priority guiding investment across the portfolio.",
                State = "Active",
            });
        }
    }

    // ---- Value-stream portfolios --------------------------------------------------------------

    private void BuildValueStreamPortfolios()
    {
        foreach (var valueStream in _org.ValueStreams)
        {
            // The portfolio's leaders are the value stream's VPs, or the ART leads when the value stream is
            // small enough to have no separate top team.
            var leadEng = valueStream.EngineeringLeadEmployeeNumber ?? valueStream.Arts.FirstOrDefault()?.EngineeringLeadEmployeeNumber;
            var leadProduct = valueStream.ProductLeadEmployeeNumber ?? valueStream.Arts.FirstOrDefault()?.ProductLeadEmployeeNumber;

            // A value-stream portfolio holds active, ongoing work, so it is active from near the window start.
            var portfolioStart = EarlyWindowDate();
            var portfolioName = AddPortfolio($"{valueStream.Domain} {Pick(PpmVocabulary.ValueStreamPortfolioSuffixes)}",
                $"Delivery portfolio for the {valueStream.Domain} value stream.",
                status: "Active", start: portfolioStart, end: null,
                sponsors: [leadProduct], owners: [leadEng], managers: [leadEng]);

            // Programs are the portfolio's thematic groupings of projects (Modernization, Integrations, …) —
            // an investment-level structure, independent of the delivery hierarchy. Build them first so the
            // ART's projects can be sorted into one by theme.
            var programs = BuildPortfolioPrograms(valueStream, portfolioName, portfolioStart, leadEng, leadProduct);

            foreach (var art in valueStream.Arts)
                BuildArtProjects(valueStream, art, portfolioName, programs);

            BuildInitiative(portfolioName, valueStream);
        }
    }

    /// <summary>
    /// Builds a portfolio's thematic programs — a handful running concurrently, each 1-3 years long and each a
    /// distinct theme, spread across the window so some have finished, some are active, and some lie ahead.
    /// Returns them so the portfolio's projects can be matched into one by theme.
    /// </summary>
    private List<GeneratedProgram> BuildPortfolioPrograms(ValueStreamNode valueStream, string portfolioName, DateTime portfolioStart, string? leadEng, string? leadProduct)
    {
        var total = DeriveProgramCount();

        // Cycle through the distinct themes so a portfolio's programs do not repeat a theme until it has used
        // them all, keeping each portfolio's program set varied.
        var themes = _faker.Random.Shuffle(PpmVocabulary.ProgramThemes).ToList();

        var programs = new List<GeneratedProgram>(total);
        for (var i = 0; i < total; i++)
        {
            var theme = themes[i % themes.Count];
            var (start, end) = ProgramWindow(portfolioStart);
            var status = StatusForWindow(start, end);

            var suffix = Pick(PpmVocabulary.ProgramNameSuffixes);
            var baseName = $"{valueStream.Domain} {theme.Name}{(suffix.Length > 0 ? $" {suffix}" : string.Empty)}";
            var name = MakeUnique(baseName, _programNames);

            // Programs are imported active and only closed by the finalize pass once all their projects are
            // closed; carry the intended status so a finalize row can be emitted after the projects land.
            AddProgram(name, $"{theme.Description} Part of the {portfolioName} portfolio.", portfolioName,
                status: "Active", start: start, end: end,
                themes: PickThemes(1),
                sponsors: [leadProduct], owners: [leadEng], managers: [leadEng]);

            programs.Add(new GeneratedProgram(name, theme, start, end, status));
        }

        return programs;
    }

    private void BuildArtProjects(ValueStreamNode valueStream, ArtNode art, string portfolioName, List<GeneratedProgram> programs)
    {
        if (art.Teams.Count == 0)
            return;

        // Projects are scoped at the ART, not the team: generate enough that roughly the requested number are
        // in flight at any time across the window (concurrency × window ÷ average duration). Each project is a
        // subset of the ART's teams collaborating, and a minority reach into another ART in the same value
        // stream.
        var projectCount = DeriveProjectCount();
        var otherArts = valueStream.Arts.Where(a => a.TeamCode != art.TeamCode).ToList();

        for (var i = 0; i < projectCount; i++)
        {
            var teams = PickParticipatingTeams(art, otherArts);
            BuildProject(teams, portfolioName, programs);
        }
    }

    /// <summary>The number of thematic programs to run in one portfolio over the window, derived from the concurrency knob and program duration.</summary>
    private int DeriveProgramCount()
    {
        const double windowMonths = (HistoryYears + RunwayYears) * 12;
        const double averageDurationMonths = (MinProgramMonths + MaxProgramMonths) / 2.0;
        var turnover = windowMonths / averageDurationMonths;
        // A theme should not repeat until the portfolio has used them all in a given period; the concurrency
        // is capped at the number of themes so concurrent programs stay distinct.
        var concurrent = Math.Min(_options.ConcurrentProgramsPerPortfolio, PpmVocabulary.ProgramThemes.Length);
        return Math.Max(1, (int)Math.Round(concurrent * turnover));
    }

    /// <summary>
    /// The number of projects to generate for one ART: enough that about <see cref="PpmOptions.ConcurrentProjectsPerArt"/>
    /// overlap at any point across the four-year window. Derived from the window length over the average
    /// project duration, so the concurrency knob reads as "in flight at once" rather than "total".
    /// </summary>
    private int DeriveProjectCount()
    {
        const double windowMonths = (HistoryYears + RunwayYears) * 12;
        const double averageDurationMonths = (MinProjectMonths + MaxProjectMonths) / 2.0;
        var turnover = windowMonths / averageDurationMonths;
        return Math.Max(1, (int)Math.Round(_options.ConcurrentProjectsPerArt * turnover));
    }

    /// <summary>
    /// Picks the teams that collaborate on one project: a subset of the owning ART's teams (a wide 1-5 spread,
    /// so the occasional solo team and the larger four/five-team project both appear), and — every now and
    /// then — one or two teams pulled from another ART in the same value stream. The first team returned is
    /// the owning team, which drives accountability and program placement.
    /// </summary>
    private IReadOnlyList<TeamNode> PickParticipatingTeams(ArtNode art, IReadOnlyList<ArtNode> otherArts)
    {
        var desired = Math.Min(_faker.Random.Int(1, 5), art.Teams.Count);
        var teams = _faker.PickRandom(art.Teams, desired).ToList();

        // ~10% of projects reach across ARTs: add a team or two from another ART in the value stream.
        if (otherArts.Count > 0 && _faker.Random.Double() < CrossArtFraction)
        {
            var otherArt = otherArts[_faker.Random.Int(0, otherArts.Count - 1)];
            if (otherArt.Teams.Count > 0)
            {
                var extra = _faker.PickRandom(otherArt.Teams, Math.Min(_faker.Random.Int(1, 2), otherArt.Teams.Count));
                teams.AddRange(extra);
            }
        }

        return teams;
    }

    // ---- Function portfolios ------------------------------------------------------------------

    private void BuildFunctionPortfolios()
    {
        var count = Math.Max(0, _options.FunctionPortfolios);
        if (count == 0)
            return;

        // Pick one naming style for the whole company so the function portfolios read coherently.
        var style = _faker.PickRandom<PpmVocabulary.PortfolioStyle>();
        var names = _faker.PickRandom(PpmVocabulary.PortfolioNamesByStyle[style], Math.Min(count, PpmVocabulary.PortfolioNamesByStyle[style].Length)).ToList();

        // Function portfolios pull projects from teams across every value stream — the cross-cutting work.
        var allTeams = _org.ValueStreams.SelectMany(vs => vs.Arts).SelectMany(a => a.Teams).ToList();
        if (allTeams.Count == 0)
            return;

        foreach (var name in names)
        {
            var portfolioStart = EarlyWindowDate();

            // Owners drawn from senior people already leading value streams, so the roles resolve.
            var leads = _org.ValueStreams
                .Select(vs => vs.EngineeringLeadEmployeeNumber ?? vs.Arts.FirstOrDefault()?.EngineeringLeadEmployeeNumber)
                .Where(n => n is not null)
                .ToList();

            // AddPortfolio dedupes the name and returns the one it stored, so projects and the initiative
            // attach to exactly the portfolio that was created.
            var portfolioName = AddPortfolio(name, $"{name}: cross-cutting investment spanning multiple value streams.",
                status: "Active", start: portfolioStart, end: null,
                sponsors: [leads.FirstOrDefault()], owners: [leads.Skip(1).FirstOrDefault() ?? leads.FirstOrDefault()], managers: []);

            // Cross-cutting, portfolio-direct projects (no program), each drawn from teams anywhere in the
            // org — these are the genuinely cross-org initiatives.
            var projectCount = _faker.Random.Int(2, 5);
            for (var i = 0; i < projectCount; i++)
            {
                var teams = _faker.PickRandom(allTeams, Math.Min(_faker.Random.Int(2, 5), allTeams.Count)).ToList();
                BuildProject(teams, portfolioName, programs: null);
            }

            BuildInitiative(portfolioName, valueStream: null);
        }
    }

    // ---- Projects -----------------------------------------------------------------------------

    /// <summary>
    /// Builds one project delivered by a set of participating teams. The first team is the owning team: it
    /// provides the Engineering Manager (owner and manager) and Product Owner (sponsor) and lends its code to
    /// the project key. Members are the union of everyone across all participating teams, so a cross-team
    /// project reads as one.
    /// <para>
    /// The project is sorted into one of the portfolio's thematic programs by its work — the leading verb of
    /// its name picks a theme (e.g. "Migrate" → Modernization) — but only a program whose own window covers
    /// the project's. When no themed program fits, or none is supplied (function portfolios), the project is
    /// portfolio-direct.
    /// </para>
    /// </summary>
    private void BuildProject(IReadOnlyList<TeamNode> teams, string portfolioName, IReadOnlyList<GeneratedProgram>? programs)
    {
        var (start, end) = ProjectWindow();
        var status = StatusForWindow(start, end);

        var owningTeam = teams[0];
        var key = ProjectKey(owningTeam);
        var verb = Pick(PpmVocabulary.ProjectVerbs);
        var name = $"{verb} {Pick(PpmVocabulary.ProjectObjects)}";

        // The owning team's EM manages and its PO sponsors; the project team is everyone across the
        // participating teams.
        var manager = owningTeam.EngineeringManagerEmployeeNumber;
        var sponsor = owningTeam.ProductOwnerEmployeeNumber;
        var members = teams.SelectMany(t => t.MemberEmployeeNumbers).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var deliveredBy = teams.Count == 1
            ? $"Delivered by the {owningTeam.Name} team."
            : $"Delivered by {teams.Count} teams, led by {owningTeam.Name}.";

        var programName = programs is null ? null : PickProgramForProject(programs, verb, start, end);

        // A lifecycle is required to approve a project or to give it tasks, so every project gets the standard one.
        _projects.Add(new ProjectCsvRow
        {
            Name = name,
            Description = $"{name}. {deliveredBy}",
            Key = key,
            PortfolioName = portfolioName,
            ExpenditureCategoryName = Pick(PpmVocabulary.ExpenditureCategories).Name,
            Status = status,
            ProgramName = programName,
            ProjectLifecycleName = PpmVocabulary.StandardLifecycle.Name,
            BusinessCase = $"Supports the {portfolioName} portfolio's goals.",
            ExpectedBenefits = "Improved efficiency, reliability and customer outcomes.",
            Start = start,
            End = end,
            StrategicThemes = Join(PickThemes(_faker.Random.Int(0, 2))),
            Sponsors = Join([sponsor]),
            Owners = Join([manager]),
            Managers = Join([manager]),
            Members = Join(members),
        });

        // Proposed/future projects have no work breakdown yet; everything else gets a dense set of tasks.
        if (!IsProposedStatus(status))
            BuildTasksForProject(key, start, end, members, status);
    }

    /// <summary>
    /// Chooses which of a portfolio's programs a project belongs to. The project's leading verb points at a
    /// theme; among the programs of that theme, only those whose own window contains the project's window are
    /// eligible (a project cannot sit in a program that had ended or not yet started). If none fits, a small
    /// share of projects stay portfolio-direct anyway, so the answer may be null.
    /// </summary>
    private string? PickProgramForProject(IReadOnlyList<GeneratedProgram> programs, string verb, DateTime projectStart, DateTime projectEnd)
    {
        // A minority of projects are standalone regardless of a matching program.
        if (_faker.Random.Double() < PortfolioDirectFraction)
            return null;

        var candidates = programs
            .Where(p => p.Theme.ProjectVerbs.Contains(verb, StringComparer.OrdinalIgnoreCase)
                && p.Start <= projectStart && p.End >= projectEnd)
            .ToList();

        // Fall back to any program whose window covers the project when no theme matched, so most projects
        // still land in a program; only when nothing overlaps is the project portfolio-direct.
        if (candidates.Count == 0)
            candidates = programs.Where(p => p.Start <= projectStart && p.End >= projectEnd).ToList();

        return candidates.Count == 0 ? null : candidates[_faker.Random.Int(0, candidates.Count - 1)].Name;
    }

    private void BuildTasksForProject(string projectKey, DateTime? projectStart, DateTime? projectEnd, IReadOnlyList<string> members, string projectStatus)
    {
        var phases = PpmVocabulary.StandardLifecycle.Phases;
        var start = projectStart ?? EarlyWindowDate();
        var end = projectEnd ?? Today;
        var completed = string.Equals(projectStatus, "Completed", StringComparison.OrdinalIgnoreCase);

        // Slice the project window across the phases in order, so tasks land in a sensible timeline.
        var totalDays = Math.Max(phases.Count, (int)(end - start).TotalDays);
        var perPhase = Math.Max(1, totalDays / phases.Count);

        for (var p = 0; p < phases.Count; p++)
        {
            var phase = phases[p];
            var phaseStart = start.AddDays(p * perPhase);
            var phaseEnd = p == phases.Count - 1 ? end : start.AddDays((p + 1) * perPhase);

            // A few root tasks per phase, each with a couple of child tasks — a dense breakdown.
            var rootCount = _faker.Random.Int(2, 3);
            for (var r = 0; r < rootCount; r++)
            {
                var rootName = MakeUniqueTaskName($"{phase.Name} workstream {r + 1}", projectKey);
                var (rootStart, rootEnd) = SubWindow(phaseStart, phaseEnd);
                AddTask(projectKey, rootName, phase.Name, parent: null, rootStart, rootEnd, members, completed, phaseEnd);

                var childCount = _faker.Random.Int(1, 3);
                for (var c = 0; c < childCount; c++)
                {
                    var childName = MakeUniqueTaskName($"{phase.Name} task {r + 1}.{c + 1}", projectKey);
                    var (childStart, childEnd) = SubWindow(rootStart, rootEnd);
                    AddTask(projectKey, childName, phase.Name, parent: rootName, childStart, childEnd, members, completed, phaseEnd);
                }
            }

            // A milestone at the end of each phase.
            var milestoneName = MakeUniqueTaskName($"{phase.Name} complete", projectKey);
            _tasks.Add(new ProjectTaskCsvRow
            {
                ProjectKey = projectKey,
                Name = milestoneName,
                Description = $"Milestone marking the end of the {phase.Name} phase.",
                PhaseName = phase.Name,
                ParentTaskName = null,
                Type = "Milestone",
                Status = completed ? "Completed" : (phaseEnd < Today ? "Completed" : "NotStarted"),
                Priority = "High",
                Progress = null,
                PlannedStart = null,
                PlannedEnd = null,
                PlannedDate = phaseEnd,
                EstimatedEffortHours = null,
                Assignees = Join([members.FirstOrDefault()]),
            });

            // The import applies phase status as data (it does not derive it from tasks), so the generator
            // rolls each phase's status up from the tasks it just emitted and records a phase row. Reading the
            // emitted task statuses back keeps the phase consistent with them by construction.
            EmitPhaseStatus(projectKey, phase.Name);
        }
    }

    /// <summary>
    /// Records a phase-status row derived from the tasks emitted for that phase: Completed when every task is
    /// closed (Completed/Cancelled) and at least one finished, Not Started when none has begun, and In
    /// Progress for anything in between.
    /// </summary>
    private void EmitPhaseStatus(string projectKey, string phaseName)
    {
        var taskStatuses = _tasks
            .Where(t => string.Equals(t.ProjectKey, projectKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.PhaseName, phaseName, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Status)
            .ToList();

        var status = RollUpPhaseStatus(taskStatuses);

        _phaseStatuses.Add(new ProjectPhaseCsvRow
        {
            ProjectKey = projectKey,
            PhaseName = phaseName,
            Status = status,
        });
    }

    private static string RollUpPhaseStatus(IReadOnlyList<string> taskStatuses)
    {
        if (taskStatuses.Count == 0)
            return "NotStarted";

        bool IsClosed(string s) => s is "Completed" or "Cancelled";

        if (taskStatuses.All(IsClosed) && taskStatuses.Any(s => s == "Completed"))
            return "Completed";

        if (taskStatuses.All(s => s == "NotStarted"))
            return "NotStarted";

        return "InProgress";
    }

    private void AddTask(string projectKey, string name, string phaseName, string? parent, DateTime start, DateTime end, IReadOnlyList<string> members, bool projectCompleted, DateTime phaseEnd)
    {
        // Progress and status follow the timeline: done in the past, in progress around now, not started later.
        var (status, progress) = projectCompleted || end < Today
            ? ("Completed", 100m)
            : start > Today
                ? ("NotStarted", 0m)
                : ("InProgress", _faker.Random.Decimal(10, 80));

        _tasks.Add(new ProjectTaskCsvRow
        {
            ProjectKey = projectKey,
            Name = name,
            Description = null,
            PhaseName = phaseName,
            ParentTaskName = parent,
            Type = "Task",
            Status = status,
            Priority = Pick(TaskPriorities),
            Progress = progress,
            PlannedStart = start,
            PlannedEnd = end,
            PlannedDate = null,
            EstimatedEffortHours = _faker.Random.Int(8, 120),
            Assignees = Join([_faker.PickRandom(members.DefaultIfEmpty(null))]),
        });
    }

    // ---- Strategic initiatives ----------------------------------------------------------------

    private void BuildInitiative(string portfolioName, ValueStreamNode? valueStream)
    {
        var name = MakeUnique(Pick(PpmVocabulary.InitiativeNames), _usedInitiativeNames);
        var (start, end) = InitiativeWindow();
        var status = StatusForWindow(start, end, forInitiative: true);

        var leadEng = valueStream?.EngineeringLeadEmployeeNumber ?? valueStream?.Arts.FirstOrDefault()?.EngineeringLeadEmployeeNumber;
        var leadProduct = valueStream?.ProductLeadEmployeeNumber ?? valueStream?.Arts.FirstOrDefault()?.ProductLeadEmployeeNumber;

        // Link a few of the portfolio's own projects to the initiative.
        var linkedKeys = _projects
            .Where(p => string.Equals(p.PortfolioName, portfolioName, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Key)
            .OrderBy(_ => _faker.Random.Int())
            .Take(_faker.Random.Int(1, 3))
            .ToList();

        _initiatives.Add(new StrategicInitiativeCsvRow
        {
            Name = name,
            Description = $"{name} across the {portfolioName} portfolio.",
            PortfolioName = portfolioName,
            Status = status,
            Start = start,
            End = end,
            ProjectKeys = Join(linkedKeys),
            Sponsors = Join([leadProduct]),
            Owners = Join([leadEng]),
        });

        // A couple of KPIs per initiative.
        foreach (var template in _faker.PickRandom(PpmVocabulary.KpiTemplates, Math.Min(2, PpmVocabulary.KpiTemplates.Length)))
        {
            _kpis.Add(new StrategicInitiativeKpiCsvRow
            {
                StrategicInitiativeName = name,
                Name = template.Name,
                Description = template.Description,
                TargetValue = template.TargetValue,
                StartingValue = template.StartingValue,
                Prefix = template.Prefix,
                Suffix = template.Suffix,
                TargetDirection = template.TargetDirection,
            });
        }
    }

    // ---- Row builders -------------------------------------------------------------------------

    private readonly HashSet<string> _portfolioNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _programNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _taskNamesByProject = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a portfolio row, deduping its name, and returns the stored name so callers reference the right one.</summary>
    private string AddPortfolio(string name, string description, string status, DateTime? start, DateTime? end,
        IReadOnlyList<string?> sponsors, IReadOnlyList<string?> owners, IReadOnlyList<string?> managers)
    {
        name = MakeUnique(name, _portfolioNames);
        _portfolios.Add(new PortfolioCsvRow
        {
            Name = name,
            Description = description,
            Status = status,
            Start = start,
            End = end,
            Sponsors = Join(sponsors),
            Owners = Join(owners),
            Managers = Join(managers),
        });
        return name;
    }

    private void AddProgram(string name, string description, string portfolioName, string status, DateTime? start, DateTime? end,
        IReadOnlyList<string> themes, IReadOnlyList<string?> sponsors, IReadOnlyList<string?> owners, IReadOnlyList<string?> managers)
    {
        _programs.Add(new ProgramCsvRow
        {
            Name = name,
            Description = description,
            PortfolioName = portfolioName,
            Status = status,
            Start = start,
            End = end,
            StrategicThemes = Join(themes),
            Sponsors = Join(sponsors),
            Owners = Join(owners),
            Managers = Join(managers),
        });
    }

    // ---- Timeline + status --------------------------------------------------------------------

    /// <summary>A project's date range: somewhere in the four-year window, 2-9 months long.</summary>
    private (DateTime Start, DateTime End) ProjectWindow()
    {
        var start = _faker.Date.Between(WindowStart, WindowEnd.AddMonths(-MinProjectMonths));
        var end = start.AddMonths(_faker.Random.Int(MinProjectMonths, MaxProjectMonths));
        if (end > WindowEnd) end = WindowEnd;
        return (start.Date, end.Date);
    }

    /// <summary>
    /// A program's date range: 1-3 years long, starting at or after the portfolio's start and never running
    /// past the window end. Programs need to overlap projects generously, so they are long and start early.
    /// </summary>
    private (DateTime Start, DateTime End) ProgramWindow(DateTime portfolioStart)
    {
        var start = _faker.Date.Between(portfolioStart, LaterOf(portfolioStart, WindowEnd.AddMonths(-MinProgramMonths))).Date;
        var end = start.AddMonths(_faker.Random.Int(MinProgramMonths, MaxProgramMonths));
        if (end > WindowEnd) end = WindowEnd;
        return (start.Date, end.Date);
    }

    /// <summary>An initiative's date range: longer than a project, spanning much of the window.</summary>
    private (DateTime Start, DateTime End) InitiativeWindow()
    {
        var start = _faker.Date.Between(WindowStart, Today.AddMonths(-3));
        var end = start.AddMonths(_faker.Random.Int(12, 30));
        if (end > WindowEnd) end = WindowEnd;
        return (start.Date, end.Date);
    }

    /// <summary>
    /// The status an item should hold given where its window sits relative to today: finished if it ended in
    /// the past, in flight if it spans today, and not yet started if it lies in the future. A minority of
    /// past work is cancelled rather than completed.
    /// </summary>
    private string StatusForWindow(DateTime start, DateTime end, bool forInitiative = false)
    {
        if (end < Today)
            return _faker.Random.Double() < 0.15 ? "Cancelled" : "Completed";

        if (start > Today)
            return forInitiative ? "Approved" : PickFutureStatus();

        return "Active";
    }

    private string PickFutureStatus() => _faker.Random.Double() < 0.5 ? "Proposed" : "Approved";

    private static bool IsClosedStatus(string status) =>
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

    private static bool IsProposedStatus(string status) =>
        string.Equals(status, "Proposed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    private DateTime EarlyWindowDate() => _faker.Date.Between(WindowStart, WindowStart.AddMonths(3)).Date;

    private (DateTime Start, DateTime End) SubWindow(DateTime start, DateTime end)
    {
        if (end <= start)
            return (start, start.AddDays(1));

        var totalDays = (int)(end - start).TotalDays;
        var s = start.AddDays(_faker.Random.Int(0, Math.Max(0, totalDays / 2)));
        var e = s.AddDays(_faker.Random.Int(1, Math.Max(1, totalDays / 2)));
        if (e > end) e = end;
        return (s.Date, e.Date);
    }

    private static DateTime LaterOf(DateTime a, DateTime b) => a > b ? a : b;

    // ---- Naming + helpers ---------------------------------------------------------------------

    private static readonly string[] TaskPriorities = ["Low", "Medium", "High", "Critical"];

    private string ProjectKey(TeamNode team)
    {
        // Base the key on the team's code, kept to the 2-20 uppercase-alphanumeric project-key format.
        var baseKey = new string(team.TeamCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (baseKey.Length < 2) baseKey = "PRJ";
        if (baseKey.Length > 12) baseKey = baseKey[..12];

        var candidate = baseKey;
        var n = 1;
        while (!_usedProjectKeys.Add(candidate))
        {
            candidate = $"{baseKey}{n}";
            if (candidate.Length > 20) candidate = $"{baseKey[..Math.Min(baseKey.Length, 16)]}{n}";
            n++;
        }
        return candidate;
    }

    private IReadOnlyList<string> PickThemes(int count)
    {
        if (count <= 0 || _themeNames.Count == 0)
            return [];

        return _faker.PickRandom(_themeNames, Math.Min(count, _themeNames.Count)).ToList();
    }

    private string MakeUniqueTaskName(string name, string projectKey)
    {
        var used = _taskNamesByProject.TryGetValue(projectKey, out var set) ? set : _taskNamesByProject[projectKey] = new(StringComparer.OrdinalIgnoreCase);

        if (used.Add(name))
            return name;

        var n = 2;
        string candidate;
        do
        {
            candidate = $"{name} ({n})";
            n++;
        }
        while (!used.Add(candidate));
        return candidate;
    }

    private static string MakeUnique(string name, HashSet<string> used)
    {
        if (used.Add(name))
            return name;

        var n = 2;
        string candidate;
        do
        {
            candidate = $"{name} {n}";
            n++;
        }
        while (!used.Add(candidate));
        return candidate;
    }

    private string Pick(string[] pool) => _faker.PickRandom(pool);
    private PpmVocabulary.ExpenditureCategoryDefinition Pick(PpmVocabulary.ExpenditureCategoryDefinition[] pool) => _faker.PickRandom(pool);

    /// <summary>Joins a set of natural keys into the semicolon-separated form the imports parse, dropping blanks and duplicates.</summary>
    private static string? Join(IEnumerable<string?> values)
    {
        var cleaned = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cleaned.Count == 0 ? null : string.Join(";", cleaned);
    }
}
