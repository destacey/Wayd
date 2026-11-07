using System.Globalization;
using Bogus;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates what the accepted and fulfilled intake requests became: a project started for some, and for others
/// scope an existing project absorbed, mapped to one to three of that project's deliverables.
/// </summary>
/// <remarks>
/// A fulfilled request was delivered, so the deliverables it maps are ones its project accepted, and its new
/// project is one that has finished; an accepted request maps whatever its project has under way. The data then
/// holds requests whose deliverables are all accepted as well as ones still waiting on them.
/// <para>
/// Draws from a stream of its own, so generating outcomes never shifts the requests or the projects.
/// </para>
/// </remarks>
public sealed class IntakeRequestOutcomeGenerator
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "demand-management.outcomes";

    public const string NewProject = "NewProject";
    public const string ExistingProjectScope = "ExistingProjectScope";

    private const string AcceptedDeliverable = "Accepted";

    private readonly GeneratedDemandManagement _demandManagement;
    private readonly GeneratedPpm _ppm;
    private readonly GenerationContext _context;
    private readonly Faker _faker;

    public IntakeRequestOutcomeGenerator(GeneratedDemandManagement demandManagement, GeneratedPpm ppm, GenerationContext context)
    {
        _demandManagement = demandManagement;
        _ppm = ppm;
        _context = context;
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    private DateTimeOffset StartOfToday => new(_context.AsOf.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);

    public IReadOnlyList<IntakeRequestOutcomeModel> Generate()
    {
        var deliverablesByProject = _ppm.ProjectDeliverables
            .GroupBy(d => d.ProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var desksByName = _demandManagement.Desks.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        HashSet<string> startedProjects = new(StringComparer.OrdinalIgnoreCase);

        List<IntakeRequestOutcomeModel> outcomes = [];
        foreach (var request in _demandManagement.Requests)
        {
            var fulfilled = request.Status == DemandManagementVocabulary.Fulfilled;
            if (!fulfilled && request.Status != DemandManagementVocabulary.Accepted)
                continue;

            // Drawn for every request, used or not, so one request's outcome never moves the next one's.
            var roll = _faker.Random.Double();
            var addedBy = request.AssigneeEmployeeNumber
                ?? (desksByName.TryGetValue(request.DeskName, out var desk) ? desk.Members.FirstOrDefault()?.EmployeeNumber : null);
            if (addedBy is null)
                continue;

            var addedOn = AddedOn(request, fulfilled);
            var outcome = roll switch
            {
                < 0.3 => StartedFor(request, fulfilled, startedProjects, addedBy, addedOn),
                < 0.8 => AbsorbedBy(request, fulfilled, deliverablesByProject, addedBy, addedOn),
                _ => null,
            };
            if (outcome is not null)
                outcomes.Add(outcome);
        }

        return outcomes;
    }

    /// <summary>
    /// A project started for the request: one created no earlier than the request, finished for a fulfilled one and
    /// still open for an accepted one, and not already started for another request.
    /// </summary>
    private IntakeRequestOutcomeModel? StartedFor(IntakeRequestModel request, bool fulfilled, HashSet<string> started,
        string addedBy, DateTimeOffset addedOn)
    {
        var submitted = DateOnly.FromDateTime(request.SubmittedAt.UtcDateTime);
        var candidates = _ppm.Projects
            .Where(p => p.CreatedOn >= submitted && !started.Contains(p.Key) && IsFinished(p.Status) == fulfilled
                && !IsCanceled(p.Status))
            .ToList();
        if (candidates.Count == 0)
            return null;

        var project = _faker.PickRandom(candidates);
        started.Add(project.Key);

        return new IntakeRequestOutcomeModel
        {
            RequestHandle = request.Handle,
            Kind = NewProject,
            ProjectKey = project.Key,
            Note = _faker.Random.Double() < 0.5 ? $"{project.Name} was started to deliver this request." : null,
            DeliverableNames = [],
            AddedByEmployeeNumber = addedBy,
            AddedOn = addedOn,
        };
    }

    /// <summary>
    /// Scope an existing project absorbed, mapped to one to three of its deliverables: accepted ones for a fulfilled
    /// request, any for an accepted one.
    /// </summary>
    private IntakeRequestOutcomeModel? AbsorbedBy(IntakeRequestModel request, bool fulfilled,
        Dictionary<string, List<ProjectDeliverableModel>> deliverablesByProject, string addedBy, DateTimeOffset addedOn)
    {
        var candidates = _ppm.Projects
            .Where(p => !IsCanceled(p.Status) && IsFinished(p.Status) == fulfilled)
            .Select(p => (Project: p, Deliverables: deliverablesByProject.GetValueOrDefault(p.Key, [])
                .Where(d => !fulfilled || d.Status == AcceptedDeliverable)
                .ToList()))
            .Where(c => c.Deliverables.Count > 0)
            .ToList();
        if (candidates.Count == 0)
            return null;

        var (project, deliverables) = _faker.PickRandom(candidates);
        var mapped = _faker.PickRandom(deliverables, Math.Min(deliverables.Count, _faker.Random.Int(1, 3)))
            .Select(d => d.Name)
            .ToList();

        return new IntakeRequestOutcomeModel
        {
            RequestHandle = request.Handle,
            Kind = ExistingProjectScope,
            ProjectKey = project.Key,
            Note = Truncate(string.Format(CultureInfo.InvariantCulture,
                "{0} absorbed this request: {1}.", project.Name, string.Join(", ", mapped)), 1024),
            DeliverableNames = mapped,
            AddedByEmployeeNumber = addedBy,
            AddedOn = addedOn,
        };
    }

    /// <summary>
    /// When the outcome was recorded: just before a fulfilled request was closed, and some days after an accepted
    /// one was taken on, never later than this morning.
    /// </summary>
    private DateTimeOffset AddedOn(IntakeRequestModel request, bool fulfilled)
    {
        var decided = request.StatusChangedAt ?? request.SubmittedAt;
        var at = fulfilled
            ? decided.AddHours(-_faker.Random.Int(1, 48))
            : decided.AddDays(_faker.Random.Int(0, 10));

        var notBefore = request.SubmittedAt.AddMinutes(1);
        if (at < notBefore)
            at = notBefore;

        return at > StartOfToday ? (StartOfToday > notBefore ? StartOfToday : notBefore) : at;
    }

    private static bool IsFinished(string status) => string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsCanceled(string status) => string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
