using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// Builds <see cref="StoryMap"/> instances for tests by populating properties through the private
/// constructor, with fluent helpers to override identity and access fields. A generated map is public,
/// editable by anyone who can view it, and owned by <see cref="StoryMapActors.Owner"/>.
/// </summary>
/// <remarks>
/// <c>Generate()</c> bypasses <see cref="StoryMap.Create"/>, so the result has no goals and — more
/// importantly — <b>no default swim lane</b>, which a real map always has. Use it for mapping and
/// identity tests; use <see cref="StoryMapFakerExtensions.CreateSeeded"/> whenever a test needs a
/// graph that behaves like a real map.
/// </remarks>
public class StoryMapFaker : PrivateConstructorFaker<StoryMap>
{
    public StoryMapFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => f.Random.Int(1, 10000));
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.Description, f => f.Lorem.Sentence(6));
        RuleFor(x => x.Status, StatusCategory.Active);
        RuleFor(x => x.Visibility, Visibility.Public);
        RuleFor(x => x.EditAccess, StoryMapEditAccess.AnyoneWhoCanView);
        RuleFor("_owners", (f, m) => new List<StoryMapOwner> { new(m.Id, StoryMapActors.OwnerEmployeeId) });
    }
}

public static class StoryMapFakerExtensions
{
    public static StoryMapFaker WithId(this StoryMapFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static StoryMapFaker WithKey(this StoryMapFaker faker, int key)
    {
        faker.RuleFor(x => x.Key, key);
        return faker;
    }

    public static StoryMapFaker WithName(this StoryMapFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static StoryMapFaker WithDescription(this StoryMapFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);
        return faker;
    }

    public static StoryMapFaker WithStatus(this StoryMapFaker faker, StatusCategory status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    public static StoryMapFaker WithVisibility(this StoryMapFaker faker, Visibility visibility)
    {
        faker.RuleFor(x => x.Visibility, visibility);
        return faker;
    }

    public static StoryMapFaker WithEditAccess(this StoryMapFaker faker, StoryMapEditAccess editAccess)
    {
        faker.RuleFor(x => x.EditAccess, editAccess);
        return faker;
    }

    public static StoryMapFaker WithOwners(this StoryMapFaker faker, params Guid[] employeeIds)
    {
        faker.RuleFor("_owners", (f, m) => employeeIds.Select(id => new StoryMapOwner(m.Id, id)).ToList());
        return faker;
    }

    public static StoryMapFaker WithEditors(this StoryMapFaker faker, params Guid[] employeeIds)
    {
        faker.RuleFor("_editors", (f, m) => employeeIds.Select(id => new StoryMapEditor(m.Id, id)).ToList());
        return faker;
    }

    public static StoryMapFaker WithEditorTeams(this StoryMapFaker faker, params Guid[] teamIds)
    {
        faker.RuleFor("_editorTeams", (f, m) => teamIds.Select(id => new StoryMapEditorTeam(m.Id, id)).ToList());
        return faker;
    }

    /// <summary>
    /// Builds a map the realistic way — via the <see cref="StoryMap.Create"/> factory — and adds one
    /// goal (with one step), so it has exactly one goal and the single default lane. Prefer this over
    /// <c>Generate()</c> when a test needs a real, mutable graph to operate on. (<c>Create</c> itself
    /// now yields an empty map; this helper seeds the first goal for tests that need one.) The map is
    /// owned by <see cref="StoryMapActors.Owner"/> unless <paramref name="ownerEmployeeId"/> is given.
    /// </summary>
    public static StoryMap CreateSeeded(
        string name = "Story Map",
        string? description = "A description",
        Guid? ownerEmployeeId = null,
        string firstGoalName = "First goal",
        string firstStepName = "First step")
    {
        var ownerId = ownerEmployeeId ?? StoryMapActors.OwnerEmployeeId;
        var map = StoryMap.Create(name, description, ownerId).Value;
        var owner = StoryMapActors.Employee(ownerId);
        var goal = map.AddGoal(firstGoalName, owner).Value;
        map.AddStep(goal.Id, firstStepName, owner);
        return map;
    }
}
