using Bogus;
using Wayd.Common.Models;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates a coherent organization directly with Bogus, shaped as a three-tier delivery hierarchy:
/// value streams (top teams-of-teams) → ARTs (mid teams-of-teams) → teams (leaves). Larger value streams
/// get the full three tiers; smaller ones collapse to a single ART over their teams — unless the options switch
/// either tier on or off outright. People are staffed by
/// tier — a team has ICs plus an engineering manager (who is also an IC there) and a product owner; an ART
/// has an engineering lead and a product lead who do not sit on any single team; a value stream has VP/Director
/// leaders. The management tree mirrors the delivery hierarchy. Emits the CSV row sets the API imports consume.
/// </summary>
public sealed class OrgGenerator
{
    private readonly OrgOptions _options;
    private readonly Faker _faker;

    private readonly List<Person> _people = [];
    private readonly List<TeamCsvRow> _teams = [];
    private readonly List<TeamMembershipCsvRow> _teamMemberships = [];
    private readonly List<TeamMemberCsvRow> _members = [];
    private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _roleNames = new(StringComparer.OrdinalIgnoreCase);

    // Names of the delivery groups that have no team of teams to name them. Planning intervals are named
    // after the group, and the import rejects two intervals of the same name.
    private readonly HashSet<string> _groupNames = new(StringComparer.OrdinalIgnoreCase);
    private int _nextEmployeeSeq = 1000;

    /// <summary>The area name this generator draws its seed under.</summary>
    public const string AreaName = "organization";

    private readonly GenerationContext _context;

    public OrgGenerator(OrgOptions options, GenerationContext context)
    {
        _options = options;
        _context = context;

        // Derived from the area name, so adding another generator does not shift this one's data.
        _faker = new Faker { Random = new Randomizer(context.SeedFor(AreaName)) };
    }

    private Person _ceo = null!;
    private Person _cto = null!;
    private Person _cpo = null!;

    // The delivery hierarchy, captured as it is built so it can be surfaced as an OrgStructure for the PPM generator.
    private readonly List<ValueStreamNode> _valueStreamNodes = [];

    public GeneratedOrg Generate()
    {
        BuildExecutiveLayer();
        BuildHierarchyAndStaff();
        BuildNonDeliveryOrganization();
        SimulateAttrition();
        AssignEmployeeTypes();

        return new GeneratedOrg(
            _people.Select(p => p.ToRow()).ToList(),
            _teams,
            _teamMemberships,
            _members,
            _roleNames.ToList(),
            new OrgStructure(
                _valueStreamNodes,
                _ceo.EmployeeNumber,
                _cto.EmployeeNumber,
                _cpo.EmployeeNumber,
                Positions()));
    }

    private Dictionary<string, IReadOnlyList<Tenure>> Positions()
    {
        var positions = new Dictionary<string, IReadOnlyList<Tenure>>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in _people.Select(p => p.Position).Distinct())
        {
            IReadOnlyList<Tenure> tenures = [.. position.Holders.Select(h => new Tenure(h.EmployeeNumber, h.HireDate, h.LeftOn))];
            foreach (var holder in position.Holders)
                positions[holder.EmployeeNumber] = tenures;
        }

        return positions;
    }

    // ---- Executive layer ----------------------------------------------------------------------

    private void BuildExecutiveLayer()
    {
        // A shared top that joins the delivery tree and the rest of the company under one company. The
        // delivery value-stream VPs report to the CTO (engineering) and CPO (product); non-delivery function
        // heads report to the CEO.
        _ceo = AddPerson(OrgVocabulary.ChiefExecutiveTitle, department: "Executive", manager: null);
        _cto = AddPerson(OrgVocabulary.ChiefTechnologyTitle, department: "Engineering", manager: _ceo);
        _cpo = AddPerson(OrgVocabulary.ChiefProductTitle, department: "Product", manager: _ceo);
    }

    // ---- Hierarchy + staffing -----------------------------------------------------------------

    private void BuildHierarchyAndStaff()
    {
        var valueStreamCount = Math.Max(1, _options.ValueStreams);
        var totalTeams = Math.Max(valueStreamCount, _options.Teams);

        // Distribute leaf teams across value streams as evenly as possible.
        var teamsPerValueStream = DistributeEvenly(totalTeams, valueStreamCount);

        foreach (var teamCount in teamsPerValueStream)
        {
            BuildValueStream(teamCount);
        }
    }

    private void BuildValueStream(int teamCount)
    {
        var domain = PickDomain();
        var activeDate = RecentActiveDate();

        // Sized as though ARTs were generated even when they are not, so a value stream's own tier is decided
        // the same way either way: one with enough teams for two ARTs is large enough to have a top team.
        var artCount = Math.Max(1, (int)Math.Round(teamCount / 3.0));
        var hasValueStreamTeam = _options.ValueStreamTier switch
        {
            StructureMode.On => true,
            StructureMode.Off => false,
            _ => artCount >= 2,
        };

        TeamNodeRef? valueStream = null;
        if (hasValueStreamTeam)
        {
            valueStream = AddTeamOfTeams($"{domain} {Pick(OrgVocabulary.ValueStreamSuffixes)}", activeDate);
            StaffValueStream(valueStream);
        }

        var artNodes = new List<ArtNode>();

        if (_options.ArtTier == StructureMode.Off)
        {
            // The teams report straight to the value stream's leaders, or to the executives when it has none,
            // and plan and ship as one group.
            var teams = BuildTeams(teamCount, domain, activeDate, valueStream);
            artNodes.Add(new ArtNode(null, valueStream?.Name ?? MakeUnique(domain, _groupNames), null, null, teams));
        }
        else
        {
            var teamsPerArt = DistributeEvenly(teamCount, artCount);
            for (var i = 0; i < artCount; i++)
            {
                // Two ARTs in one value stream need a word between the domain and the suffix to tell them apart.
                var artName = artCount >= 2
                    ? $"{domain} {PickDistinctDomainWord()} {Pick(OrgVocabulary.ArtSuffixes)}"
                    : $"{domain} {Pick(OrgVocabulary.ArtSuffixes)}";
                var art = AddTeamOfTeams(artName, activeDate);

                // Wire the parent before staffing so ART leaders can report up to the value-stream leaders.
                if (valueStream is not null)
                    LinkMembership(art, valueStream, activeDate);

                StaffArt(art);

                artNodes.Add(new ArtNode(
                    art.Code,
                    art.Name,
                    art.EngineeringLead?.EmployeeNumber,
                    art.ProductLead?.EmployeeNumber,
                    BuildTeams(teamsPerArt[i], domain, activeDate, art)));
            }
        }

        _valueStreamNodes.Add(new ValueStreamNode(
            domain,
            valueStream?.Code,
            valueStream?.EngineeringLead?.EmployeeNumber,
            valueStream?.ProductLead?.EmployeeNumber,
            artNodes));
    }

    /// <summary>Leaf teams under <paramref name="parent"/>, or at the top of the hierarchy when there is none.</summary>
    private List<TeamNode> BuildTeams(int count, string domain, DateOnly activeDate, TeamNodeRef? parent)
    {
        var teamNodes = new List<TeamNode>(count);
        foreach (var _ in Enumerable.Range(0, count))
        {
            var team = AddTeam($"{domain} {PickDistinctDomainWord()} {Pick(OrgVocabulary.Functions)}", activeDate);
            if (parent is not null)
                LinkMembership(team, parent, activeDate);

            StaffTeam(team, parent);
            teamNodes.Add(ToTeamNode(team));
        }

        return teamNodes;
    }

    private static TeamNode ToTeamNode(TeamNodeRef team) => new(
        team.Code,
        team.Name,
        team.EngineeringManager?.EmployeeNumber,
        team.ProductOwner?.EmployeeNumber,
        team.Members.Select(m => m.EmployeeNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    // ---- Non-delivery organization ------------------------------------------------------------

    private void BuildNonDeliveryOrganization()
    {
        var ratio = Math.Clamp(_options.EffectiveDeliveryRatio, 0.01, 0.99);

        // "Inside" = people staffed onto a delivery team/ART/value stream. Size the rest of the company so the
        // inside share matches the target ratio. Everyone not staffed (execs + non-delivery) is "outside".
        var insideCount = _people.Count(StaffedSomewhere);
        var targetTotal = (int)Math.Round(insideCount / ratio);
        // Everyone already created but unstaffed (the executives) counts toward the outside population.
        var outsideToCreate = Math.Max(0, targetTotal - _people.Count);

        if (outsideToCreate == 0)
            return;

        // Distribute the outside headcount across the non-delivery functions by weight.
        var functions = OrgVocabulary.NonDeliveryFunctions;
        var totalWeight = functions.Sum(f => f.Weight);

        foreach (var (function, weight) in functions)
        {
            var count = (int)Math.Round(outsideToCreate * (weight / totalWeight));
            if (count <= 0)
                continue;

            BuildNonDeliveryFunction(function, count);
        }
    }

    private void BuildNonDeliveryFunction(OrgVocabulary.NonDeliveryFunction function, int headcount)
    {
        // Head of the function reports to the CEO. These people hold no team memberships.
        var head = AddPerson(function.HeadTitle, function.Department, manager: _ceo);
        var remaining = headcount - 1;
        if (remaining <= 0)
            return;

        // A shallow chain: a few managers under the head, with individual contributors under each manager.
        var managerCount = Math.Max(1, remaining / 6);
        var managers = new List<Person>(managerCount);
        for (var i = 0; i < managerCount && remaining > 0; i++)
        {
            managers.Add(AddPerson(function.ManagerTitle, function.Department, manager: head));
            remaining--;
        }

        var m = 0;
        while (remaining > 0)
        {
            var manager = managers[m % managers.Count];
            var title = _faker.PickRandom(function.IndividualTitles);
            AddPerson(title, function.Department, manager: manager);
            remaining--;
            m++;
        }
    }

    // ---- Tier staffing ------------------------------------------------------------------------

    private void StaffTeam(TeamNodeRef team, TeamNodeRef? parent)
    {
        // A single-team engineering manager who is ALSO an individual contributor on that team. They report to
        // whoever leads the team of teams above, and to the CTO/CPO when nothing is above.
        var em = AddPerson(jobTitle: "Engineering Manager", department: "Engineering", manager: parent?.EngineeringLead ?? _cto);
        AddMembership(team, em, OrgVocabulary.EngineeringManagerRole);

        // Also contributes as an IC — in a discipline they do not already hold. "Engineering Manager" is
        // in the discipline pool as well as being the leadership role above, so an unfiltered draw can
        // land on the role this person was just given and emit the same staffing fact twice.
        AddMembership(team, em, PickExcept(OrgVocabulary.Roles, OrgVocabulary.EngineeringManagerRole));
        team.EngineeringManager = em;
        team.Members.Add(em);

        // A product manager acting as product owner on the team.
        var po = AddPerson(jobTitle: "Product Manager", department: "Product", manager: parent?.ProductLead ?? _cpo);
        AddMembership(team, po, OrgVocabulary.ProductOwnerRole);
        team.ProductOwner = po;
        team.Members.Add(po);

        // A handful of individual contributors reporting to the team's EM.
        var icCount = _faker.Random.Int(3, 6);
        for (var i = 0; i < icCount; i++)
        {
            var discipline = Pick(OrgVocabulary.Roles);
            var ic = AddPerson(jobTitle: _faker.PickRandom(OrgVocabulary.IndividualTitles), department: "Engineering", manager: em);
            AddMembership(team, ic, discipline);
            team.Members.Add(ic);
        }
    }

    private void StaffArt(TeamNodeRef art)
    {
        // An engineering lead over the ART (manages the teams' EMs; not on any single team) and a product lead.
        // In a 3-tier value stream they report to the value-stream VPs; in a 2-tier org (no value-stream ToT)
        // they report straight to the CTO/CPO.
        var engLead = AddPerson(jobTitle: "Senior Engineering Manager", department: "Engineering", manager: art.Parent?.EngineeringLead ?? _cto);
        AddMembership(art, engLead, OrgVocabulary.ArtEngineeringLeadRole);
        art.EngineeringLead = engLead;

        var productLead = AddPerson(jobTitle: "Group Product Manager", department: "Product", manager: art.Parent?.ProductLead ?? _cpo);
        AddMembership(art, productLead, OrgVocabulary.ArtProductLeadRole);
        art.ProductLead = productLead;
    }

    private void StaffValueStream(TeamNodeRef valueStream)
    {
        // VP/Director-level leaders sit on the value stream (the top team of teams) and report to the CTO/CPO.
        var vpEng = AddPerson(jobTitle: "VP of Engineering", department: "Engineering", manager: _cto);
        AddMembership(valueStream, vpEng, OrgVocabulary.ValueStreamEngineeringLeadRole);
        valueStream.EngineeringLead = vpEng;

        var vpProduct = AddPerson(jobTitle: "VP of Product", department: "Product", manager: _cpo);
        AddMembership(valueStream, vpProduct, OrgVocabulary.ValueStreamProductLeadRole);
        valueStream.ProductLead = vpProduct;
    }

    // ---- People -------------------------------------------------------------------------------

    /// <summary>A job someone holds: whoever holds it today, and everyone who held it before them, oldest first.</summary>
    private sealed class Position
    {
        public Position? ManagerPosition { get; init; }
        public List<Person> Holders { get; } = [];

        /// <summary>Who was in post on a day, with the same rule as <see cref="OrgStructure.HolderOn"/>.</summary>
        public Person HolderOn(DateOnly on) => Holders.LastOrDefault(h => h.HireDate <= on) ?? Holders[0];
    }

    private sealed class Person
    {
        public required string EmployeeNumber { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required DateOnly HireDate { get; set; }
        public required string JobTitle { get; init; }
        public required string Department { get; init; }
        public required Position Position { get; init; }
        public string? ManagerNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public DateOnly? LeftOn { get; set; }
        public string EmployeeType { get; set; } = OrgVocabulary.RegularEmployeeType;

        public EmployeeCsvRow ToRow() => new()
        {
            ImportId = EmployeeNumber,
            EmployeeNumber = EmployeeNumber,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            HireDate = HireDate.ToDateTime(TimeOnly.MinValue),
            JobTitle = JobTitle,
            Department = Department,
            OfficeLocation = null,
            ManagerNumber = ManagerNumber,
            IsActive = IsActive,
            EmployeeType = EmployeeType,
        };
    }

    private Person AddPerson(string jobTitle, string department, Person? manager)
    {
        var first = _faker.Name.FirstName();
        var last = _faker.Name.LastName();

        var person = new Person
        {
            EmployeeNumber = $"E-{_nextEmployeeSeq++:D5}",
            FirstName = first,
            LastName = last,
            Email = UniqueEmail(first, last),
            // Hires span anywhere from founding to the run's today. The reference date has to be passed
            // in: left out, Bogus measures back from its own DateTime.Now, so a pinned seed still moves.
            HireDate = DateOnly.FromDateTime(_faker.Date.Past(_context.CompanyAgeYears, _context.AsOf.ToDateTime(TimeOnly.MinValue))),
            JobTitle = jobTitle,
            Department = department,
            Position = new Position { ManagerPosition = manager?.Position },
            ManagerNumber = manager?.EmployeeNumber,
        };

        person.Position.Holders.Add(person);
        _people.Add(person);
        return person;
    }

    // ---- Attrition ----------------------------------------------------------------------------

    // A replacement starts a few weeks after the person they replace leaves. Kept shorter than the shortest
    // project, so a project can never fall entirely inside the gap and name nobody who worked on it.
    private const int MinBackfillDays = 14;
    private const int MaxBackfillDays = 45;

    // Departures from one position are at least three months apart. A replacement starts up to MaxBackfillDays
    // after the departure, so the shortest tenure is this less that — still longer than the backfill itself.
    private const int MinTenureDays = 90;

    /// <summary>
    /// Plays the delivery history forward for every position: the people who held it and left, each replaced
    /// by the next, ending with whoever holds it today.
    /// </summary>
    /// <remarks>
    /// Leavers are not a slice of today's roster marked inactive. A position that lost its engineering manager
    /// still has one, so attrition adds people rather than removing them — which is what lets it reach the
    /// delivery teams at all: memberships are current state, so a leaver simply holds none.
    /// <para>
    /// Leaving is memoryless, so the gaps between departures are drawn from an exponential distribution and a
    /// position loses, on average, the annual rate times the years of history. Measured over the history
    /// rather than the company's whole age, which would bury an old enterprise under decades of ghosts.
    /// </para>
    /// <para>
    /// Positions are walked in the order they were created, which puts every manager's position before its
    /// reports': a leaver reported to whoever held their manager's position on the day they left.
    /// </para>
    /// </remarks>
    private void SimulateAttrition()
    {
        var historyStart = _context.WindowStart > _context.FoundedOn ? _context.WindowStart : _context.FoundedOn;
        var delivery = DeliveryPositions();
        List<Person> leavers = [];

        foreach (var current in _people.ToList())
        {
            var position = current.Position;
            var inDelivery = delivery.Contains(position);
            var departures = Departures(historyStart);

            // Delivery records name whoever held a position on their own dates, which reach back to the start of
            // the history, so somebody must already have held it by then. A later hire would leave that history
            // owned by nobody who worked there.
            if (departures.Count == 0)
            {
                if (inDelivery && current.HireDate > historyStart)
                    current.HireDate = Between(_context.FoundedOn, historyStart);

                continue;
            }

            var hired = Between(_context.FoundedOn, inDelivery ? historyStart : departures[0].AddDays(-MinTenureDays));
            position.Holders.Clear();

            foreach (var leftOn in departures)
            {
                var leaver = AddLeaver(current, hired, leftOn);
                position.Holders.Add(leaver);
                leavers.Add(leaver);

                hired = leftOn.AddDays(_faker.Random.Int(MinBackfillDays, MaxBackfillDays));
            }

            current.HireDate = hired;
            position.Holders.Add(current);
        }

        _people.AddRange(leavers);
    }

    /// <summary>The days a position's holders left it, oldest first, all within the history.</summary>
    private List<DateOnly> Departures(DateOnly historyStart)
    {
        List<DateOnly> departures = [];
        if (_options.AttritionRate <= 0)
            return departures;

        // Walked back from the latest day a replacement could still have started by today.
        var cursor = _context.AsOf.AddDays(-MaxBackfillDays);
        while (true)
        {
            var years = -Math.Log(1 - _faker.Random.Double()) / _options.AttritionRate;
            cursor = cursor.AddDays(-Math.Max(MinTenureDays, (int)(years * 365.25)));

            if (cursor < historyStart.AddDays(MinTenureDays))
                break;

            departures.Add(cursor);
        }

        departures.Reverse();
        return departures;
    }

    /// <summary>Someone who held <paramref name="successor"/>'s position before them, and left it.</summary>
    private Person AddLeaver(Person successor, DateOnly hired, DateOnly leftOn)
    {
        var first = _faker.Name.FirstName();
        var last = _faker.Name.LastName();

        return new Person
        {
            EmployeeNumber = $"E-{_nextEmployeeSeq++:D5}",
            FirstName = first,
            LastName = last,
            Email = UniqueEmail(first, last),
            HireDate = hired,
            JobTitle = successor.JobTitle,
            Department = successor.Department,
            Position = successor.Position,
            ManagerNumber = successor.Position.ManagerPosition?.HolderOn(leftOn).EmployeeNumber,
            IsActive = false,
            LeftOn = leftOn,
        };
    }

    /// <summary>
    /// The positions a delivery record can name: everyone staffed on a team, plus the CTO and CPO, who lead a
    /// value stream's work when nothing sits between them and its teams.
    /// </summary>
    private HashSet<Position> DeliveryPositions()
    {
        var staffed = _members.Select(m => m.EmployeeNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. _people.Where(p => staffed.Contains(p.EmployeeNumber)).Select(p => p.Position),
            _cto.Position,
            _cpo.Position,
        ];
    }

    private DateOnly Between(DateOnly start, DateOnly end) =>
        end <= start ? start : start.AddDays(_faker.Random.Int(0, end.DayNumber - start.DayNumber));

    private void AssignEmployeeTypes()
    {
        // Everyone defaults to a regular "Employee". Only a minority of non-manager individual contributors
        // are non-regular (contractor, intern, …). Managers are always regular: someone who manages people is
        // never a contractor/intern in this model. Decided by position, so whoever held a manager's position
        // before today's holder was a regular employee too, whether or not anyone still reports to them.
        var managerNumbers = ManagerNumbers();
        var managerPositions = _people.Where(p => managerNumbers.Contains(p.EmployeeNumber)).Select(p => p.Position).ToHashSet();

        foreach (var person in _people)
        {
            if (managerPositions.Contains(person.Position))
                continue; // managers stay regular

            // ~12% of individual contributors are a non-regular worker type.
            if (_faker.Random.Double() < 0.12)
                person.EmployeeType = _faker.PickRandom(OrgVocabulary.NonRegularEmployeeTypes);
        }
    }

    /// <summary>The employee numbers of everyone who manages at least one other person.</summary>
    private HashSet<string> ManagerNumbers() => _people
        .Where(p => p.ManagerNumber is not null)
        .Select(p => p.ManagerNumber!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool StaffedSomewhere(Person person) =>
        _members.Any(m => m.EmployeeNumber == person.EmployeeNumber);

    /// <summary>
    /// A work address for someone, unique across the company.
    /// </summary>
    /// <remarks>
    /// Characters an address may not contain are dropped from the name; an apostrophe is kept, because an
    /// address may legitimately contain one and a seeded O'Connell is what proves it. That is not
    /// hypothetical — a live run produced exactly that name and could not create a sign-in for them, which
    /// is how the username rule was found to be narrower than the address grammar.
    /// </remarks>
    private string UniqueEmail(string first, string last)
    {
        var baseLocal = new string([.. $"{first}.{last}".ToLowerInvariant().Where(IsAddressCharacter)]);
        var candidate = $"{baseLocal}@acme.example";
        var suffix = 1;
        while (!_usedEmails.Add(candidate))
        {
            candidate = $"{baseLocal}{suffix}@acme.example";
            suffix++;
        }
        return candidate;
    }

    /// <summary>
    /// Whether a character may appear in the local part of an email address.
    /// </summary>
    /// <remarks>
    /// Taken from <see cref="EmailAddress.AllowedCharacters"/> rather than restated, so the generator
    /// cannot drift from the grammar the API validates against — which is the whole reason that constant
    /// exists. Everything but the @ separating the parts, since this only filters the local part.
    /// <para>
    /// Stricter than a <c>char.IsLetterOrDigit</c> test in one useful way: that accepts letters outside
    /// ASCII, and an accented name would produce an address the API's own format check rejects.
    /// </para>
    /// </remarks>
    private static bool IsAddressCharacter(char c) =>
        EmailAddress.AllowedCharacters.Contains(c) && c != '@';

    // ---- Teams --------------------------------------------------------------------------------

    /// <summary>A generated team or team-of-teams, plus the tier leaders staffed on it (for manager wiring).</summary>
    private sealed class TeamNodeRef
    {
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required DateOnly ActiveDate { get; init; }
        public TeamNodeRef? Parent { get; set; }
        public Person? EngineeringLead { get; set; }
        public Person? ProductLead { get; set; }

        // Populated for leaf teams only, so the PPM generator can staff projects from a team's own people.
        public Person? EngineeringManager { get; set; }
        public Person? ProductOwner { get; set; }
        public List<Person> Members { get; } = [];
    }

    private TeamNodeRef AddTeam(string name, DateOnly activeDate)
    {
        name = MakeUnique(name, _usedNames);
        var code = ResolveCode(name, _usedCodes);
        _teams.Add(new TeamCsvRow
        {
            ImportId = code,
            Type = "Team",
            Name = name,
            Code = code,
            Description = null,
            ActiveDate = activeDate,
            IsActive = true,
            InactiveDate = null,
        });
        return new TeamNodeRef { Code = code, Name = name, ActiveDate = activeDate };
    }

    private TeamNodeRef AddTeamOfTeams(string name, DateOnly activeDate)
    {
        name = MakeUnique(name, _usedNames);
        var code = ResolveCode(name, _usedCodes);
        _teams.Add(new TeamCsvRow
        {
            ImportId = code,
            Type = "TeamOfTeams",
            Name = name,
            Code = code,
            Description = null,
            ActiveDate = activeDate,
            IsActive = true,
            InactiveDate = null,
        });
        return new TeamNodeRef { Code = code, Name = name, ActiveDate = activeDate };
    }

    private void LinkMembership(TeamNodeRef child, TeamNodeRef parent, DateOnly activeDate)
    {
        child.Parent = parent;

        // The membership must start on or after both teams' active dates; use the later of the two.
        var start = activeDate > parent.ActiveDate ? activeDate : parent.ActiveDate;
        _teamMemberships.Add(new TeamMembershipCsvRow
        {
            ImportId = $"{child.Code}|{parent.Code}",
            ChildCode = child.Code,
            ParentCode = parent.Code,
            Start = start,
            End = null,
        });
    }

    private void AddMembership(TeamNodeRef team, Person person, string roleName)
    {
        _roleNames.Add(roleName);
        _members.Add(new TeamMemberCsvRow
        {
            // One person can hold several roles on a team, and each is its own row — so the role has to be
            // part of the key or the second row would collide with the first.
            ImportId = $"{team.Code}|{person.EmployeeNumber}|{roleName}",
            TeamCode = team.Code,
            EmployeeNumber = person.EmployeeNumber,
            RoleName = roleName,
        });
    }

    // ---- Naming + codes -----------------------------------------------------------------------

    private string PickDomain() => Pick(OrgVocabulary.Domains);

    private string PickDistinctDomainWord() => Pick(OrgVocabulary.Domains);

    private string Pick(string[] pool) => _faker.PickRandom(pool);

    /// <summary>Draws from the pool, never returning the one value the caller already used.</summary>
    private string PickExcept(string[] pool, string excluded) =>
        _faker.PickRandom(pool.Where(p => !string.Equals(p, excluded, StringComparison.OrdinalIgnoreCase)).ToArray());

    /// <summary>
    /// When a team was stood up: within the current structure's age, which is shorter than the company's.
    /// </summary>
    /// <remarks>
    /// The reference date is passed explicitly for the same reason as hire dates — Bogus otherwise counts
    /// back from its own DateTime.Now, which moves every run regardless of the seed.
    /// </remarks>
    private DateOnly RecentActiveDate() =>
        DateOnly.FromDateTime(_faker.Date.Past(_context.TeamStructureAgeYears, _context.AsOf.ToDateTime(TimeOnly.MinValue)));

    private string MakeUnique(string name, HashSet<string> used)
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

    private static string ResolveCode(string name, HashSet<string> usedCodes)
    {
        // Team codes are uppercase letters/numbers, 2-10 chars. Build initials from the name, number to unique.
        var initials = new string(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 0 && char.IsLetter(w[0]))
            .Select(w => char.ToUpperInvariant(w[0]))
            .ToArray());

        var letters = new string(name.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var seed = initials.Length >= 2 ? initials : (letters.Length >= 2 ? letters[..Math.Min(4, letters.Length)] : "TM");
        seed = seed.Length > 8 ? seed[..8] : seed;

        var candidate = seed;
        var n = 1;
        while (candidate.Length < 2 || !usedCodes.Add(candidate))
        {
            candidate = $"{seed}{n}";
            if (candidate.Length > 10)
                candidate = $"{seed[..Math.Min(seed.Length, 8)]}{n}";
            n++;
        }

        return candidate;
    }

    private static int[] DistributeEvenly(int total, int buckets)
    {
        var result = new int[buckets];
        for (var i = 0; i < buckets; i++)
            result[i] = total / buckets + (i < total % buckets ? 1 : 0);
        return result;
    }
}
