using Wayd.Common.Domain.Enums.Work;
using Wayd.Planning.Domain.Models.StoryMaps;
using Wayd.TestData.Core;

namespace Wayd.Planning.Domain.Tests.Data;

/// <summary>
/// Builds <see cref="StoryMap"/> instances for tests by populating properties through the private
/// constructor, with fluent helpers to override identity fields.
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
        RuleFor(x => x.OwnerId, f => f.Random.Guid().ToString());
        RuleFor(x => x.Status, WorkStatusCategory.Active);
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

    public static StoryMapFaker WithOwnerId(this StoryMapFaker faker, string ownerId)
    {
        faker.RuleFor(x => x.OwnerId, ownerId);
        return faker;
    }

    public static StoryMapFaker WithStatus(this StoryMapFaker faker, WorkStatusCategory status)
    {
        faker.RuleFor(x => x.Status, status);
        return faker;
    }

    /// <summary>
    /// Builds a map the realistic way — via the <see cref="StoryMap.Create"/> factory — and adds one
    /// goal (with one step), so it has exactly one goal and the single default lane. Prefer this over
    /// <c>Generate()</c> when a test needs a real, mutable graph to operate on. (<c>Create</c> itself
    /// now yields an empty map; this helper seeds the first goal for tests that need one.)
    /// </summary>
    public static StoryMap CreateSeeded(
        string name = "Story Map",
        string? description = "A description",
        string? ownerId = null,
        string firstGoalName = "First goal",
        string firstStepName = "First step")
    {
        var map = StoryMap.Create(name, description, ownerId ?? Guid.NewGuid().ToString()).Value;
        var goal = map.AddGoal(firstGoalName).Value;
        map.AddStep(goal.Id, firstStepName);
        return map;
    }
}
