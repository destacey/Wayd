using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models;
using Wayd.Infrastructure.Persistence.Configuration;
using Wayd.Work.Domain.Models;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Keeps ownership stored the same way on every record that has it: four columns on the record's own
/// table, as <see cref="OwnershipEntityBuilderExtensions.ConfigureOwnershipInfo{TEntity}"/> maps them.
/// </summary>
/// <remarks>
/// Code that finds a record by its external reference, and migrations that move records between owners,
/// rely on the columns having one name, type and length everywhere. A configuration that maps
/// <see cref="OwnershipInfo"/> by hand compiles and runs, and drifts the first time one side is changed.
/// </remarks>
public class OwnershipMappingTests
{
    /// <summary>What <c>ConfigureOwnershipInfo</c> produces for each member, as column name, type and nullability.</summary>
    private static readonly Dictionary<string, (string Column, string Type, bool Nullable)> ExpectedColumns = new()
    {
        [nameof(OwnershipInfo.Ownership)] = (OwnershipEntityBuilderExtensions.OwnershipColumn, "varchar(32)", false),
        [nameof(OwnershipInfo.Connector)] = (OwnershipEntityBuilderExtensions.ConnectorColumn, "varchar(32)", true),
        [nameof(OwnershipInfo.SystemId)] = (OwnershipEntityBuilderExtensions.SystemIdColumn, "varchar(64)", true),
        [nameof(OwnershipInfo.ExternalId)] = (OwnershipEntityBuilderExtensions.ExternalIdColumn, "varchar(64)", true),
    };

    [Fact]
    public void EveryRecordWithOwnershipInfo_MapsItWithTheSharedColumns()
    {
        // Arrange
        var owners = AssemblyHelper.GetDomainAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IHasOwnershipInfo).IsAssignableFrom(t))
            .ToList();

        // Act
        var problems = owners.SelectMany(MappingProblems).ToList();

        // Assert
        // Workspace proves the scan sees the domain's types, so an empty problem list is a finding rather
        // than a type-matching failure.
        owners.Should().Contain(typeof(Workspace));
        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a record implementing IHasOwnershipInfo must call builder.ConfigureOwnershipInfo() in its entity " +
            "configuration and not map OwnershipInfo by hand");
    }

    private static IEnumerable<string> MappingProblems(Type owner)
    {
        var entity = WaydModel.Model.FindEntityType(owner);
        if (entity is null)
        {
            yield return $"{owner.Name} is not an entity in the model.";
            yield break;
        }

        var complex = entity.FindComplexProperty(nameof(IHasOwnershipInfo.OwnershipInfo));
        if (complex is null)
        {
            yield return $"{owner.Name}.OwnershipInfo is not mapped as a complex property.";
            yield break;
        }

        var mapped = complex.ComplexType.GetProperties().ToDictionary(p => p.Name);

        foreach (var unexpected in mapped.Keys.Except(ExpectedColumns.Keys))
            yield return $"{owner.Name}.OwnershipInfo maps {unexpected}, which the shared mapping does not.";

        foreach (var (member, expected) in ExpectedColumns)
        {
            if (!mapped.TryGetValue(member, out var property))
            {
                yield return $"{owner.Name}.OwnershipInfo.{member} is not mapped.";
                continue;
            }

            var actual = (property.GetColumnName(), property.GetColumnType(), property.IsNullable);
            if (actual != expected)
                yield return $"{owner.Name}.OwnershipInfo.{member} is {actual}, expected {expected}.";
        }
    }
}
