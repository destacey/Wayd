using Bogus;
using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Generates a coherent organization directly with Bogus, shaped as a three-tier delivery hierarchy:
/// value streams (top teams-of-teams) → ARTs (mid teams-of-teams) → teams (leaves). Larger value streams
/// get the full three tiers; smaller ones collapse to a single ART over their teams. People are staffed by
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
    private int _nextEmployeeSeq = 1000;

    public OrgGenerator(OrgOptions options)
    {
        _options = options;
        // Always drive Bogus from an explicit seed so every run is reproducible. Callers that do not supply
        // one should resolve a random seed up front (and log it) rather than leaving it null.
        var seed = options.Seed ?? Random.Shared.Next();
        _faker = new Faker { Random = new Randomizer(seed) };
    }

    private Person _ceo = null!;
    private Person _cto = null!;
    private Person _cpo = null!;

    public GeneratedOrg Generate()
    {
        BuildExecutiveLayer();
        BuildHierarchyAndStaff();
        BuildNonDeliveryOrganization();
        AssignEmployeeTypes();
        MarkFormerEmployees();

        return new GeneratedOrg(
            _people.Select(p => p.ToRow()).ToList(),
            _teams,
            _teamMemberships,
            _members,
            _roleNames.ToList());
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

        // "Large" value streams (enough teams for 2+ ARTs) get the full three tiers; small ones collapse to a
        // single ART over their teams (two tiers), so there is no separate value-stream ToT.
        var artCount = Math.Max(1, (int)Math.Round(teamCount / 3.0));
        var isThreeTier = artCount >= 2;

        TeamNodeRef? valueStream = null;
        if (isThreeTier)
        {
            valueStream = AddTeamOfTeams($"{domain} {Pick(OrgVocabulary.ValueStreamSuffixes)}", activeDate);
            StaffValueStream(valueStream);
        }

        var teamsPerArt = DistributeEvenly(teamCount, artCount);
        for (var i = 0; i < artCount; i++)
        {
            var artName = isThreeTier
                ? $"{domain} {PickDistinctDomainWord()} {Pick(OrgVocabulary.ArtSuffixes)}"
                : $"{domain} {Pick(OrgVocabulary.ArtSuffixes)}";
            var art = AddTeamOfTeams(artName, activeDate);

            // Wire the parent before staffing so ART leaders can report up to the value-stream leaders.
            if (valueStream is not null)
                LinkMembership(art, valueStream, activeDate);

            StaffArt(art);

            foreach (var _ in Enumerable.Range(0, teamsPerArt[i]))
            {
                var team = AddTeam($"{domain} {PickDistinctDomainWord()} {Pick(OrgVocabulary.Functions)}", activeDate);
                LinkMembership(team, art, activeDate);
                StaffTeam(team, art);
            }
        }
    }

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

    private void StaffTeam(TeamNodeRef team, TeamNodeRef art)
    {
        // A single-team engineering manager who is ALSO an individual contributor on that team.
        var em = AddPerson(jobTitle: "Engineering Manager", department: "Engineering", manager: art.EngineeringLead);
        AddMembership(team, em, OrgVocabulary.EngineeringManagerRole);
        AddMembership(team, em, Pick(OrgVocabulary.Roles)); // also contributes as an IC

        // A product manager acting as product owner on the team.
        var po = AddPerson(jobTitle: "Product Manager", department: "Product", manager: art.ProductLead);
        AddMembership(team, po, OrgVocabulary.ProductOwnerRole);

        // A handful of individual contributors reporting to the team's EM.
        var icCount = _faker.Random.Int(3, 6);
        for (var i = 0; i < icCount; i++)
        {
            var discipline = Pick(OrgVocabulary.Roles);
            var ic = AddPerson(jobTitle: _faker.PickRandom(OrgVocabulary.IndividualTitles), department: "Engineering", manager: em);
            AddMembership(team, ic, discipline);
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

    private sealed class Person
    {
        public required string EmployeeNumber { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required DateTime HireDate { get; init; }
        public required string JobTitle { get; init; }
        public required string Department { get; init; }
        public string? ManagerNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public string EmployeeType { get; set; } = OrgVocabulary.RegularEmployeeType;

        public EmployeeCsvRow ToRow() => new()
        {
            EmployeeNumber = EmployeeNumber,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            HireDate = HireDate,
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
            // The company is ~5 years old, so hires span anywhere from founding to now.
            HireDate = _faker.Date.Past(CompanyAgeYears),
            JobTitle = jobTitle,
            Department = department,
            ManagerNumber = manager?.EmployeeNumber,
        };

        _people.Add(person);
        return person;
    }

    private void AssignEmployeeTypes()
    {
        // Everyone defaults to a regular "Employee". Only a minority of non-manager individual contributors
        // are non-regular (contractor, intern, …). Managers are always regular: someone who manages people is
        // never a contractor/intern in this model.
        var managerNumbers = ManagerNumbers();

        foreach (var person in _people)
        {
            if (managerNumbers.Contains(person.EmployeeNumber))
                continue; // managers stay regular

            // ~12% of individual contributors are a non-regular worker type.
            if (_faker.Random.Double() < 0.12)
                person.EmployeeType = _faker.PickRandom(OrgVocabulary.NonRegularEmployeeTypes);
        }
    }

    private void MarkFormerEmployees()
    {
        if (_options.FormerEmployeeFraction <= 0)
            return;

        // Over the company's ~5-year life some employees have left. A former employee is inactive, and the
        // domain forbids inactive people from holding current team memberships, so only leaf individual
        // contributors who are not staffed on a team and who manage no one are eligible — this keeps the
        // current team structure intact and never leaves a dangling manager reference. In practice these are
        // the non-delivery individual contributors.
        var managerNumbers = ManagerNumbers();

        foreach (var person in _people)
        {
            if (managerNumbers.Contains(person.EmployeeNumber))
                continue; // this person manages someone — keep active

            if (StaffedSomewhere(person))
                continue; // keep people who hold a team role active (imports reject inactive members)

            if (_faker.Random.Double() < _options.FormerEmployeeFraction)
                person.IsActive = false;
        }
    }

    /// <summary>The employee numbers of everyone who manages at least one other person.</summary>
    private HashSet<string> ManagerNumbers() => _people
        .Where(p => p.ManagerNumber is not null)
        .Select(p => p.ManagerNumber!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool StaffedSomewhere(Person person) =>
        _members.Any(m => m.EmployeeNumber == person.EmployeeNumber);

    private string UniqueEmail(string first, string last)
    {
        var baseLocal = $"{first}.{last}".ToLowerInvariant().Replace(" ", string.Empty);
        var candidate = $"{baseLocal}@acme.example";
        var suffix = 1;
        while (!_usedEmails.Add(candidate))
        {
            candidate = $"{baseLocal}{suffix}@acme.example";
            suffix++;
        }
        return candidate;
    }

    // ---- Teams --------------------------------------------------------------------------------

    /// <summary>A generated team or team-of-teams, plus the tier leaders staffed on it (for manager wiring).</summary>
    private sealed class TeamNodeRef
    {
        public required string Code { get; init; }
        public required DateTime ActiveDate { get; init; }
        public TeamNodeRef? Parent { get; set; }
        public Person? EngineeringLead { get; set; }
        public Person? ProductLead { get; set; }
    }

    private TeamNodeRef AddTeam(string name, DateTime activeDate)
    {
        name = MakeUnique(name, _usedNames);
        var code = ResolveCode(name, _usedCodes);
        _teams.Add(new TeamCsvRow
        {
            Type = "Team",
            Name = name,
            Code = code,
            Description = null,
            ActiveDate = activeDate,
            IsActive = true,
            InactiveDate = null,
        });
        return new TeamNodeRef { Code = code, ActiveDate = activeDate };
    }

    private TeamNodeRef AddTeamOfTeams(string name, DateTime activeDate)
    {
        name = MakeUnique(name, _usedNames);
        var code = ResolveCode(name, _usedCodes);
        _teams.Add(new TeamCsvRow
        {
            Type = "TeamOfTeams",
            Name = name,
            Code = code,
            Description = null,
            ActiveDate = activeDate,
            IsActive = true,
            InactiveDate = null,
        });
        return new TeamNodeRef { Code = code, ActiveDate = activeDate };
    }

    private void LinkMembership(TeamNodeRef child, TeamNodeRef parent, DateTime activeDate)
    {
        child.Parent = parent;

        // The membership must start on or after both teams' active dates; use the later of the two.
        var start = activeDate > parent.ActiveDate ? activeDate : parent.ActiveDate;
        _teamMemberships.Add(new TeamMembershipCsvRow
        {
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
            TeamCode = team.Code,
            EmployeeNumber = person.EmployeeNumber,
            RoleName = roleName,
        });
    }

    // ---- Naming + codes -----------------------------------------------------------------------

    private string PickDomain() => Pick(OrgVocabulary.Domains);

    private string PickDistinctDomainWord() => Pick(OrgVocabulary.Domains);

    private string Pick(string[] pool) => _faker.PickRandom(pool);

    // The company is ~5 years old; the current team structure was set up ~2 years ago.
    private const int CompanyAgeYears = 5;
    private const int TeamStructureAgeYears = 2;

    private DateTime RecentActiveDate() => DateTime.SpecifyKind(_faker.Date.Past(TeamStructureAgeYears), DateTimeKind.Utc).Date;

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
