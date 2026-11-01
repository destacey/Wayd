using System.Globalization;
using Bogus;
using Wayd.Tools.DataGeneration.Cli.Csv;
using static Wayd.Tools.DataGeneration.Cli.Generation.DemandManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates the company's Request Desk: the desks that review demand, the tag axes they file it by, and the
/// requests the company has submitted to them over its history.
/// <para>
/// Each value stream gets a product desk worked by its product and engineering leads. An IT desk and a
/// Security and Compliance desk exist where the company has an IT or Legal function to staff them; the
/// second is private, since findings and audits are not for everyone to read.
/// </para>
/// <para>
/// Requests are raised by the departments that would raise them — sales asks arrive from Sales, regulatory
/// ones from Legal — and land on the desk their kind belongs to. How far each got through review depends on
/// its age: last week's are mostly still waiting, last year's mostly decided.
/// </para>
/// </summary>
public sealed class DemandManagementGenerator
{
    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "demand-management";

    private const double AverageDaysPerMonth = 30.44;

    private readonly GeneratedOrg _org;
    private readonly DemandManagementOptions _options;
    private readonly GenerationContext _context;
    private readonly Faker _faker;
    private readonly Dictionary<string, EmployeeCsvRow> _employees;

    private readonly List<IntakeDeskModel> _desks = [];
    private readonly List<IntakeRequestModel> _requests = [];

    /// <summary>The desk each kind of ask goes to; product asks pick among the value streams' desks.</summary>
    private readonly List<(IntakeDeskModel Desk, string Domain)> _productDesks = [];
    private IntakeDeskModel? _itDesk;
    private IntakeDeskModel? _securityDesk;

    public DemandManagementGenerator(GeneratedOrg org, DemandManagementOptions options, GenerationContext context)
    {
        _org = org;
        _options = options;
        _context = context;
        _employees = org.Employees.ToDictionary(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase);

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    private DateOnly Today => _context.AsOf;

    private DateOnly HistoryStart => _context.WindowStart > _context.FoundedOn ? _context.WindowStart : _context.FoundedOn;

    public GeneratedDemandManagement Generate()
    {
        BuildDesks();

        if (_desks.Count > 0)
            BuildRequests();

        return new GeneratedDemandManagement(_desks, [Size, Theme], _requests);
    }

    // ---- Desks --------------------------------------------------------------------------------

    private void BuildDesks()
    {
        var active = _org.Employees.Where(e => e.IsActive).ToList();

        // Two value streams can share a domain; they share its desk rather than each opening one of the same name.
        foreach (var domain in _org.Structure.ValueStreams.GroupBy(v => v.Domain, StringComparer.OrdinalIgnoreCase))
        {
            var valueStreams = domain.ToList();
            var admins = valueStreams.Select(v => v.ProductLeadEmployeeNumber)
                .Concat(valueStreams.SelectMany(v => v.Arts).Select(a => a.ProductLeadEmployeeNumber))
                .Concat(valueStreams.SelectMany(v => v.Arts).SelectMany(a => a.Teams).Select(t => t.ProductOwnerEmployeeNumber))
                .OfType<string>()
                .Take(1);
            var agents = valueStreams.SelectMany(v => v.Arts.Select(a => a.ProductLeadEmployeeNumber)
                    .Concat([v.EngineeringLeadEmployeeNumber])
                    .Concat(v.Arts.SelectMany(a => a.Teams).Select(t => t.ProductOwnerEmployeeNumber).Take(2)))
                .OfType<string>();

            var desk = AddDesk(
                $"{domain.Key} Product Desk",
                $"Ask for a new capability or a change to the {domain.Key} products: something customers need, a problem they keep hitting, or an idea worth trying.",
                "Open",
                admins,
                agents);
            if (desk is not null)
                _productDesks.Add((desk, domain.Key));
        }

        var it = active.Where(e => e.Department == "IT Operations").ToList();
        _itDesk = AddDesk(
            "IT Desk",
            "Ask for a change to the tools and systems the company runs on: internal applications, integrations between them, reports and automation.",
            "SummaryOnly",
            it.Where(e => e.JobTitle == "VP of IT").Select(e => e.EmployeeNumber),
            it.Where(e => e.JobTitle == "IT Manager").Select(e => e.EmployeeNumber).Take(3));

        var legal = active.Where(e => e.Department == "Legal").ToList();
        var security = active.Where(e => e.JobTitle is "VP of IT" or "Network Engineer").Select(e => e.EmployeeNumber).Take(2);
        _securityDesk = AddDesk(
            "Security and Compliance Desk",
            "Raise something security or compliance needs to look at: an audit finding, a regulatory requirement, a security risk or a control that has to change.",
            "Private",
            legal.Where(e => e.JobTitle == "General Counsel").Select(e => e.EmployeeNumber),
            legal.Where(e => e.JobTitle is "Legal Operations Manager" or "Compliance Analyst").Select(e => e.EmployeeNumber).Take(2).Concat(security));
    }

    /// <summary>
    /// Adds a desk staffed by the admins and agents named, or nothing when nobody is left to staff it — a desk
    /// with no members is where requests go to be forgotten.
    /// </summary>
    private IntakeDeskModel? AddDesk(string name, string description, string visibility, IEnumerable<string> admins, IEnumerable<string> agents)
    {
        List<IntakeDeskMemberModel> members = [];
        foreach (var number in admins)
            Add(number, "Admin");
        foreach (var number in agents)
            Add(number, "Agent");

        if (members.Count == 0)
            return null;

        // A desk run by agents alone has nobody to change its settings, so its first member runs it.
        if (members.All(m => m.Role != "Admin"))
            members[0] = members[0] with { Role = "Admin" };

        var desk = new IntakeDeskModel { Name = name, Description = description, Visibility = visibility, Members = members };
        _desks.Add(desk);
        return desk;

        void Add(string number, string role)
        {
            if (!members.Any(m => string.Equals(m.EmployeeNumber, number, StringComparison.OrdinalIgnoreCase)))
                members.Add(new IntakeDeskMemberModel(number, role));
        }
    }

    // ---- Requests -----------------------------------------------------------------------------

    private void BuildRequests()
    {
        var lastDay = Today.AddDays(-1);
        if (lastDay < HistoryStart)
            return;

        var months = (lastDay.DayNumber - HistoryStart.DayNumber + 1) / AverageDaysPerMonth;
        var count = (int)Math.Round(_options.RequestsPerMonth * months);

        var kinds = Kinds.Where(k => DeskFor(k.Desk) is not null || _productDesks.Count > 0).ToArray();
        if (kinds.Length == 0)
            return;

        var days = Enumerable.Range(0, count)
            .Select(_ => Between(HistoryStart, lastDay))
            .Order()
            .ToList();

        // Bogus reads weights as probabilities, so they have to add up to one.
        var total = kinds.Sum(k => k.Weight);
        float[] weights = [.. kinds.Select(k => (float)(k.Weight / total))];

        foreach (var day in days)
        {
            var kind = _faker.Random.WeightedRandom(kinds, weights);
            AddRequest(kind, day);
        }
    }

    private void AddRequest(RequestKind kind, DateOnly day)
    {
        (IntakeDeskModel Desk, string Domain) target = DeskFor(kind.Desk) is { } fixedDesk
            ? (fixedDesk, _faker.PickRandom<ValueStreamNode>(_org.Structure.ValueStreams).Domain)
            : _faker.PickRandom<(IntakeDeskModel, string)>(_productDesks);
        var (desk, domain) = target;

        var requester = Requester(kind, day);
        if (requester is null)
            return;

        var filler = _faker.PickRandom(kind.Fillers);
        var submittedAt = At(day, _faker.Random.Int(8, 17));
        var handle = $"REQ-{_requests.Count + 1:D5}";

        var (status, changedOn, reason) = Review(day);
        var reviewed = status != Submitted;
        var needBy = NeedBy(kind, day, filler, desk);
        var fromServiceDesk = kind.Channel is Support or BusinessStakeholder && _faker.Random.Double() < 0.4;

        _requests.Add(new IntakeRequestModel
        {
            Handle = handle,
            Title = Truncate(Format(_faker.PickRandom(kind.Titles), domain, filler), 256),
            Description = Format(_faker.PickRandom(kind.Descriptions), domain, filler),
            ExpectedBenefits = _faker.Random.Double() < 0.75 ? Format(_faker.PickRandom(kind.Benefits), domain, filler) : null,
            DeskName = desk.Name,
            ChannelName = kind.Channel,
            RequesterEmployeeNumber = requester,
            SubmittedAt = submittedAt,
            NeedByDate = needBy?.Date,
            NeedByBasis = needBy?.Basis,
            NeedByReference = needBy?.Reference,
            NeedByCommittedByEmployeeNumber = needBy?.CommittedBy,
            NeedByCommittedOn = needBy?.CommittedOn,
            OriginSourceSystem = fromServiceDesk ? ServiceDesk : null,
            OriginExternalId = fromServiceDesk ? $"RITM{_requests.Count + 1:D7}" : null,
            OriginUrl = fromServiceDesk ? $"{ServiceDeskUrl}RITM{_requests.Count + 1:D7}" : null,
            Status = status,
            StatusChangedAt = changedOn is { } on ? Later(At(on, _faker.Random.Int(9, 17)), submittedAt.AddMinutes(5)) : null,
            StatusReason = reason,
            AssigneeEmployeeNumber = _faker.Random.Double() < (reviewed ? 0.85 : 0.2) ? Assignee(desk, day) : null,
            Tags = reviewed && _faker.Random.Double() < 0.75 ? Tags() : [],
        });
    }

    /// <summary>
    /// A member of the desk who already worked here on the day, or nobody. The import records the assignment,
    /// tags and status moves as theirs, so a member hired since would appear in the history before they joined.
    /// </summary>
    private string? Assignee(IntakeDeskModel desk, DateOnly day)
    {
        var members = MembersOn(desk, day).ToList();
        return members.Count == 0 ? null : _faker.PickRandom<IntakeDeskMemberModel>(members).EmployeeNumber;
    }

    private IEnumerable<IntakeDeskMemberModel> MembersOn(IntakeDeskModel desk, DateOnly day) =>
        desk.Members.Where(m => _employees.TryGetValue(m.EmployeeNumber, out var e) && WasEmployedOn(e, day));

    private IntakeDeskModel? DeskFor(DeskKind kind) => kind switch
    {
        DeskKind.It => _itDesk,
        DeskKind.SecurityAndCompliance => _securityDesk,
        _ => null,
    };

    /// <summary>
    /// Someone from one of the kind's departments who worked here on the day, or anyone who did when the
    /// company has nobody in those departments.
    /// </summary>
    private string? Requester(RequestKind kind, DateOnly day)
    {
        var employed = _org.Employees.Where(e => WasEmployedOn(e, day)).ToList();
        var fromDepartment = employed.Where(e => kind.Departments.Contains(e.Department)).ToList();
        var pool = fromDepartment.Count > 0 ? fromDepartment : employed;

        return pool.Count == 0 ? null : _faker.PickRandom(pool).EmployeeNumber;
    }

    private bool WasEmployedOn(EmployeeCsvRow employee, DateOnly day)
    {
        if (employee.HireDate is not { } hired || DateOnly.FromDateTime(hired) > day)
            return false;

        return _org.Structure.TenureOf(employee.EmployeeNumber) is { } tenure
            ? tenure.LeftOn is not { } left || left > day
            : employee.IsActive;
    }

    /// <summary>
    /// Where a request submitted on the day has got to by today: a status of the default workflow, the day it
    /// moved there, and the reason the requester reads where the status needs one.
    /// </summary>
    private (string Status, DateOnly? ChangedOn, string? Reason) Review(DateOnly submitted)
    {
        var age = Today.DayNumber - submitted.DayNumber;

        string status = age switch
        {
            < 14 => _faker.Random.WeightedRandom([Submitted, InReview, WaitingOnRequester], [0.6f, 0.3f, 0.1f]),
            < 60 => _faker.Random.WeightedRandom(
                [Submitted, InReview, WaitingOnRequester, Accepted, Declined, Withdrawn], [0.1f, 0.35f, 0.1f, 0.3f, 0.12f, 0.03f]),
            < 180 => _faker.Random.WeightedRandom(
                [InReview, Accepted, Fulfilled, Declined, Withdrawn], [0.08f, 0.4f, 0.22f, 0.25f, 0.05f]),
            _ => _faker.Random.WeightedRandom(
                [Accepted, Fulfilled, Declined, Withdrawn], [0.2f, 0.45f, 0.28f, 0.07f]),
        };

        if (status == Submitted)
            return (status, null, null);

        // Triage is quick; a decision takes longer, and delivery longer still.
        var (minDays, maxDays) = status switch
        {
            InReview or WaitingOnRequester => (1, 10),
            Accepted or Declined or Withdrawn => (5, 45),
            _ => (30, 150),
        };
        var changedOn = Earlier(submitted.AddDays(_faker.Random.Int(minDays, maxDays)), Today.AddDays(-1));
        if (changedOn < submitted)
            changedOn = submitted;

        var reason = status switch
        {
            Declined => _faker.PickRandom(DeclineReasons),
            Fulfilled => _faker.PickRandom(FulfilledReasons),
            Withdrawn when _faker.Random.Bool() => _faker.PickRandom(WithdrawnNotes),
            _ => null,
        };

        return (status, changedOn, reason);
    }

    private sealed record NeedByModel(DateOnly Date, string Basis, string Reference, string? CommittedBy, DateOnly? CommittedOn);

    /// <summary>
    /// When the ask has to land, and why. A regulatory ask always has a date the regulation sets; a sales ask
    /// often has one in the contract; anything else sometimes carries a date someone chose.
    /// </summary>
    private NeedByModel? NeedBy(RequestKind kind, DateOnly submitted, string filler, IntakeDeskModel desk)
    {
        var date = submitted.AddDays(_faker.Random.Int(45, 300));

        if (kind.Channel == Regulatory)
            return new(date, "Regulatory", Format(_faker.PickRandom(RegulatoryReferences), filler), null, null);

        if (kind.Channel == Sales && _faker.Random.Double() < 0.5)
            return new(date, "Contractual", Format(_faker.PickRandom(ContractualReferences), filler), null, null);

        var roll = _faker.Random.Double();
        if (roll < 0.08)
            return new(date, "ExternalEvent", _faker.PickRandom(ExternalEventReferences), null, null);

        if (roll < 0.16)
        {
            // Whoever ran the desk on the day the request came in made the commitment, then and there.
            var members = MembersOn(desk, submitted).ToList();
            if ((members.FirstOrDefault(m => m.Role == "Admin") ?? members.FirstOrDefault()) is { } committer)
                return new(date, "Commitment", _faker.PickRandom(CommitmentReferences), committer.EmployeeNumber, submitted);
        }

        if (roll < 0.35)
            return new(date, "RequesterPreference", _faker.PickRandom(PreferenceReferences), null, null);

        return null;
    }

    private IReadOnlyList<string> Tags()
    {
        List<string> tags = [$"{Size.Name}|{_faker.PickRandom<string>(Size.Tags)}"];
        tags.AddRange(_faker.Random.ListItems(Theme.Tags.ToList(), _faker.Random.Int(1, 2)).Select(t => $"{Theme.Name}|{t}"));
        return tags;
    }

    // ---- Timeline -----------------------------------------------------------------------------

    private DateOnly Between(DateOnly from, DateOnly to) =>
        to <= from ? from : from.AddDays(_faker.Random.Int(0, to.DayNumber - from.DayNumber));

    private DateTimeOffset At(DateOnly day, int hour) =>
        new(day.ToDateTime(new TimeOnly(hour, _faker.Random.Int(0, 59))), TimeSpan.Zero);

    private static DateOnly Earlier(DateOnly a, DateOnly b) => a < b ? a : b;

    private static DateTimeOffset Later(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static string Format(string template, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, template, values);

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
