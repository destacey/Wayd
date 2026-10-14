using System.Collections;
using System.Reflection;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Dtos;

public class TeamCapacityDtoTests
{
    [Theory]
    [InlineData(typeof(TeamCapacityDto))]
    [InlineData(typeof(TeamCapacityRollupDto))]
    public void TeamCapacity_HasNowhereToCarryWhySomeoneIsAway(Type capacityDto)
    {
        // Arrange — everyone who can see a team's capacity reads these, and only a few may know the reason
        HashSet<Type> seen = [];

        // Act
        var reachable = Reachable(capacityDto, seen).ToList();

        // Assert
        reachable.Should().Contain(typeof(TeamCapacityAllocationDto), "the walk must reach the nested DTOs to prove anything");
        reachable.Should().NotContain(typeof(TimeAwayReason));
        reachable.Where(t => t.Namespace == typeof(TeamCapacityDto).Namespace)
            .SelectMany(t => t.GetProperties())
            .Select(p => p.Name)
            .Should().NotContain(name => name.Contains("Reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TeamCapacityPerson_CarriesTimeAwayAsDaysAndShareOnly()
    {
        // Arrange
        var timeAway = typeof(TeamCapacityPersonDto).GetProperty(nameof(TeamCapacityPersonDto.TimeAway))!;

        // Act
        var properties = timeAway.PropertyType.GetGenericArguments().Single().GetProperties().Select(p => p.Name);

        // Assert
        properties.Should().BeEquivalentTo(
            nameof(TeamCapacityTimeAwayDto.Start),
            nameof(TeamCapacityTimeAwayDto.End),
            nameof(TeamCapacityTimeAwayDto.PercentAway));
    }

    /// <summary>Every type a serialized <paramref name="type"/> can hold, through properties, collections and nullables.</summary>
    private static IEnumerable<Type> Reachable(Type type, HashSet<Type> seen)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            foreach (var reached in type.GetGenericArguments().SelectMany(argument => Reachable(argument, seen)))
                yield return reached;
            yield break;
        }

        if (!seen.Add(type))
            yield break;

        yield return type;

        if (type.Assembly != typeof(TeamCapacityDto).Assembly && type.Assembly != typeof(TimeAwayReason).Assembly)
            yield break;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var reached in Reachable(property.PropertyType, seen))
                yield return reached;
        }
    }
}
