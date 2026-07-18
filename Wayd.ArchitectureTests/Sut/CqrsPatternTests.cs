using CSharpFunctionalExtensions;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using NetArchTest.Rules;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Tests to enforce CQRS (Command Query Responsibility Segregation) pattern conventions.
/// These tests ensure that commands, queries, and handlers follow consistent naming and structural patterns.
///
/// CQRS Conventions Enforced:
///
/// Commands:
/// - Must end with "Command"
/// - Should be in {Feature}/Commands/ folders
/// - Should be public classes
/// - Should not contain business logic (that's in handlers)
///
/// Queries:
/// - Must end with "Query"
/// - Should be in {Feature}/Queries/ folders
/// - Should be public classes
/// - Should not contain business logic (that's in handlers)
///
/// Command Handlers:
/// - Must end with "CommandHandler" or "Handler" (for commands)
/// - Should be in the same folder as their command
/// - Should implement ICommandHandler interface
/// - Must be public (Wolverine's generated code cannot invoke internal handlers)
///
/// Query Handlers:
/// - Must end with "QueryHandler" or "Handler" (for queries)
/// - Should be in the same folder as their query
/// - Should implement IQueryHandler interface
/// - Must be public (Wolverine's generated code cannot invoke internal handlers)
///
/// General Handler Rules:
/// - Handlers should not reference other handlers directly
/// - Each command/query must have exactly one corresponding handler (Wolverine silently combines duplicates)
/// </summary>
public class CqrsPatternTests
{
    #region Command Naming Tests

    [Fact]
    public void Commands_ShouldEndWithCommand()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find commands that don't end with Command
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var commandsWithoutCommandSuffix = allTypes
            .Where(IsCommand)
            .Where(t => !t.Name.EndsWith("Command"))
            .ToList();

        // Assert
        commandsWithoutCommandSuffix.Should().BeEmpty(
            "All command classes should end with 'Command'. Violating types: {0}",
            string.Join(", ", commandsWithoutCommandSuffix.Select(t => t.FullName)));
    }

    [Fact]
    public void Commands_ShouldBePublic()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find commands that implement ICommand or ICommand<> interface and are NOT public
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var nonPublicCommands = allTypes
            .Where(IsCommand)
            .Where(t => !t.IsPublic && !t.IsNestedPublic)
            .ToList();

        // Assert
        nonPublicCommands.Should().BeEmpty(
            "All command classes should be public. Violating types: {0}",
            string.Join(", ", nonPublicCommands.Select(t => t.FullName)));
    }

    [Fact]
    public void Commands_ShouldBeSealed()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find commands that implement ICommand or ICommand<> interface and are NOT sealed
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var commands = allTypes
            .Where(IsCommand)
            .Where(t => !t.IsSealed && !t.IsAbstract)
            .ToList();

        // Assert
        commands.Should().BeEmpty(
            "All command classes should be sealed to prevent inheritance. Violating types: {0}",
            string.Join(", ", commands.Select(t => t.FullName)));
    }

    [Fact]
    public void Commands_ShouldBeRecords()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find commands that implement ICommand or ICommand<> interface and are NOT records
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var commands = allTypes
            .Where(IsCommand)
            .Where(t => !IsRecord(t))
            .ToList();

        // Assert
        commands.Should().BeEmpty(
            "All command classes should be records for immutability and value semantics. Violating types: {0}",
            string.Join(", ", commands.Select(t => t.FullName)));
    }

    #endregion

    #region Query Naming Tests

    [Fact]
    public void Queries_ShouldEndWithQuery()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find queries that don't end with Query
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var queriesWithoutQuerySuffix = allTypes
            .Where(IsQuery)
            .Where(t => !t.Name.EndsWith("Query"))
            .ToList();

        // Assert
        queriesWithoutQuerySuffix.Should().BeEmpty(
            "All query classes should end with 'Query'. Violating types: {0}",
            string.Join(", ", queriesWithoutQuerySuffix.Select(t => t.FullName)));
    }

    [Fact]
    public void Queries_ShouldBePublic()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find queries that implement IQuery<> interface and are NOT public
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var nonPublicQueries = allTypes
            .Where(IsQuery)
            .Where(t => !t.IsPublic && !t.IsNestedPublic)
            .ToList();

        // Assert
        nonPublicQueries.Should().BeEmpty(
            "All query classes should be public. Violating types: {0}",
            string.Join(", ", nonPublicQueries.Select(t => t.FullName)));
    }

    [Fact]
    public void Queries_ShouldBeSealed()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find queries that implement IQuery<> interface and are NOT sealed
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var queries = allTypes
            .Where(IsQuery)
            .Where(t => !t.IsSealed && !t.IsAbstract)
            .ToList();

        // Assert
        queries.Should().BeEmpty(
            "All query classes should be sealed to prevent inheritance. Violating types: {0}",
            string.Join(", ", queries.Select(t => t.FullName)));
    }

    [Fact]
    public void Queries_ShouldBeRecords()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find queries that implement IQuery<> interface and are NOT records
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var queries = allTypes
            .Where(IsQuery)
            .Where(t => !IsRecord(t))
            .ToList();

        // Assert
        queries.Should().BeEmpty(
            "All query classes should be records for immutability and value semantics. Violating types: {0}",
            string.Join(", ", queries.Select(t => t.FullName)));
    }

    #endregion

    #region Handler Naming Tests

    [Fact]
    public void CommandHandlers_ShouldEndWithHandler()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find command handlers that don't end with Handler
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var commandHandlersWithoutHandlerSuffix = allTypes
            .Where(IsCommandHandler)
            .Where(t => !t.Name.EndsWith("Handler"))
            .ToList();

        // Assert
        commandHandlersWithoutHandlerSuffix.Should().BeEmpty(
            "All command handler classes should end with 'Handler'. Violating types: {0}",
            string.Join(", ", commandHandlersWithoutHandlerSuffix.Select(t => t.FullName)));
    }

    [Fact]
    public void QueryHandlers_ShouldEndWithHandler()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find query handlers that don't end with Handler
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var queryHandlersWithoutHandlerSuffix = allTypes
            .Where(IsQueryHandler)
            .Where(t => !t.Name.EndsWith("Handler"))
            .ToList();

        // Assert
        queryHandlersWithoutHandlerSuffix.Should().BeEmpty(
            "All query handler classes should end with 'Handler'. Violating types: {0}",
            string.Join(", ", queryHandlersWithoutHandlerSuffix.Select(t => t.FullName)));
    }

    [Fact]
    public void Handlers_ShouldBePublic()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Find all command and query handlers that are NOT public. Wolverine's code generation
        // news up handlers from a separate (generated) assembly, so it cannot reference internal types
        // — every command/query handler must be public.
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var allHandlers = allTypes
            .Where(t => IsCommandHandler(t) || IsQueryHandler(t))
            .ToList();

        var nonPublicHandlers = allHandlers
            .Where(t => !(t.IsPublic || t.IsNestedPublic))
            .ToList();

        // Assert
        nonPublicHandlers.Should().BeEmpty(
            "All command/query handler classes must be public so Wolverine's generated code can invoke them. Violating types: {0}",
            string.Join(", ", nonPublicHandlers.Select(t => t.FullName)));
    }

    [Fact]
    public void EachCommandOrQuery_ShouldHaveExactlyOneHandler()
    {
        // Arrange - Wolverine silently COMBINES multiple handlers for the same message (MediatR instead
        // rejected them at registration). This test restores that safety: exactly one handler per
        // command/query message type.
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();
        var infrastructureAssemblies = AssemblyHelper.GetInfrastructureAssemblies();
        var assemblies = applicationAssemblies.Concat(infrastructureAssemblies).Distinct().ToArray();

        var allTypes = Types.InAssemblies(assemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var handlers = allTypes
            .Where(t => IsCommandHandler(t) || IsQueryHandler(t))
            .ToList();

        // Act - Map each handled message type to the handlers that handle it.
        var handlersByMessage = handlers
            .SelectMany(h => HandledMessageTypes(h).Select(m => (Message: m, Handler: h)))
            .GroupBy(x => x.Message)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.FullName} -> [{string.Join(", ", g.Select(x => x.Handler.FullName))}]")
            .ToList();

        // Assert
        handlersByMessage.Should().BeEmpty(
            "Each command/query must have exactly one handler; Wolverine would otherwise silently combine them. Duplicates: {0}",
            string.Join("; ", handlersByMessage));
    }

    #endregion

    #region Handler Isolation Tests

    [Fact]
    public void Handlers_ShouldNotDependOnOtherHandlers()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();
        var allTypes = Types.InAssemblies(applicationAssemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .ToList();

        var allHandlers = allTypes
            .Where(t => IsCommandHandler(t) || IsQueryHandler(t))
            .ToList();

        var violations = new List<string>();

        // Act - Check each handler to see if it depends on OTHER handlers (not itself)
        foreach (var handler in allHandlers)
        {
            // Get the types this handler references
            var referencedTypes = handler.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Select(f => f.FieldType)
                .Concat(handler.GetProperties().Select(p => p.PropertyType))
                .Concat(handler.GetConstructors()
                    .SelectMany(c => c.GetParameters().Select(p => p.ParameterType)))
                .ToList();

            // Check if any referenced types are OTHER handlers
            foreach (var referencedType in referencedTypes)
            {
                var otherHandler = allHandlers.FirstOrDefault(h =>
                    h != handler &&
                    (h == referencedType || h.FullName == referencedType.FullName));

                if (otherHandler != null)
                {
                    violations.Add($"{handler.FullName} depends on {otherHandler.FullName}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "Handlers should not depend on other handlers directly. Dispatch commands/queries through IDispatcher instead. Violations: {0}",
            string.Join("; ", violations));
    }

    #endregion

    #region Event Handler Tests

    [Fact]
    public void EventHandlers_ShouldEndWithHandler()
    {
        // Arrange
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();

        // Act - Classes in EventHandlers namespace should end with Handler
        var result = Types.InAssemblies(applicationAssemblies)
            .That()
            .ResideInNamespace("EventHandlers")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Handler")
            .Or()
            .HaveNameEndingWith("EventHandler")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All event handler classes should end with 'Handler' or 'EventHandler'. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    #endregion

    #region Wolverine Safety Tests

    [Fact]
    public void CommandHandlerHandleMethods_ShouldReturnResultOrResultOfT()
    {
        // Arrange - Under Wolverine, a value returned from a handler that is published (rather than
        // invoked) becomes a cascaded message. Every command handler must return Task<Result> or
        // Task<Result<T>> so nothing is ever accidentally re-published as a new message. (Query handlers
        // legitimately return Task<TResponse> — they are only ever invoked, never published.)
        var applicationAssemblies = AssemblyHelper.GetApplicationAssemblies();
        var infrastructureAssemblies = AssemblyHelper.GetInfrastructureAssemblies();
        var assemblies = applicationAssemblies.Concat(infrastructureAssemblies).Distinct().ToArray();

        var commandHandlers = Types.InAssemblies(assemblies)
            .That()
            .AreClasses()
            .GetTypes()
            .Where(IsCommandHandler)
            .ToList();

        // Act
        var violations = new List<string>();
        foreach (var handler in commandHandlers)
        {
            var handleMethods = handler.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(m => m.Name == "Handle");

            foreach (var method in handleMethods)
            {
                if (!ReturnsResult(method.ReturnType))
                {
                    violations.Add($"{handler.FullName}.Handle -> {method.ReturnType.Name}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "Command handler Handle methods must return Task<Result> or Task<Result<T>>. Violations: {0}",
            string.Join("; ", violations));
    }

    [Fact]
    public void OnlyTheDispatchSeams_ShouldDependOnIMessageBus()
    {
        // Arrange - Call sites dispatch through IDispatcher and publish events through IEventPublisher.
        // Wolverine's IMessageBus is an implementation detail that must stay confined to the seams that
        // wrap it, so no handler or controller can accidentally call PublishAsync (which would turn a
        // command/query into a fire-and-forget message with different failure semantics).
        var allAssemblies = AssemblyHelper.GetApplicationAssemblies()
            .Concat(AssemblyHelper.GetInfrastructureAssemblies())
            .Distinct()
            .ToArray();

        // The only types allowed to reference IMessageBus directly.
        var allowed = new[]
        {
            "WolverineDispatcher",     // IDispatcher implementation
            "EventPublisher",          // IEventPublisher implementation
            "WolverineConfiguration",  // host bootstrap
        };

        // Act
        var offenders = Types.InAssemblies(allAssemblies)
            .That()
            .AreClasses()
            .And()
            .DoNotHaveName(allowed)
            .Should()
            .NotHaveDependencyOn("Wolverine.IMessageBus")
            .GetResult();

        // Assert
        offenders.IsSuccessful.Should().BeTrue(
            "Only the dispatch/publish seams may depend on Wolverine's IMessageBus. Violating types: {0}",
            string.Join(", ", offenders.FailingTypeNames ?? []));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// True when <paramref name="returnType"/> is Task&lt;Result&gt; or Task&lt;Result&lt;T&gt;&gt;.
    /// </summary>
    private static bool ReturnsResult(Type returnType)
    {
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return false;
        }

        var inner = returnType.GetGenericArguments()[0];
        if (inner == typeof(Result))
        {
            return true;
        }

        return inner.IsGenericType && inner.GetGenericTypeDefinition() == typeof(Result<>);
    }

    /// <summary>
    /// Checks if a type implements ICommand&lt;TResponse&gt; interface.
    /// </summary>
    private static bool IsCommand(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition().Name == "ICommand`1");
    }

    /// <summary>
    /// Checks if a type implements IQuery&lt;TResponse&gt; interface.
    /// </summary>
    private static bool IsQuery(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition().Name == "IQuery`1");
    }

    /// <summary>
    /// Checks if a type implements ICommandHandler&lt;TCommand&gt; or ICommandHandler&lt;TCommand, TResponse&gt; interface.
    /// </summary>
    private static bool IsCommandHandler(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType &&
            (i.GetGenericTypeDefinition().Name == "ICommandHandler`1" ||
             i.GetGenericTypeDefinition().Name == "ICommandHandler`2"));
    }

    /// <summary>
    /// Checks if a type implements IQueryHandler&lt;TQuery, TResponse&gt; interface.
    /// </summary>
    private static bool IsQueryHandler(Type type)
    {
        return type.GetInterfaces().Any(i =>
            i.IsGenericType &&
            i.GetGenericTypeDefinition().Name == "IQueryHandler`2");
    }

    /// <summary>
    /// Returns the command/query message type(s) a handler handles, read from its
    /// ICommandHandler&lt;&gt;/IQueryHandler&lt;&gt; interface implementations (the message is always the
    /// first generic argument).
    /// </summary>
    private static IEnumerable<Type> HandledMessageTypes(Type handler)
    {
        return handler.GetInterfaces()
            .Where(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition().Name == "ICommandHandler`1" ||
                 i.GetGenericTypeDefinition().Name == "ICommandHandler`2" ||
                 i.GetGenericTypeDefinition().Name == "IQueryHandler`2"))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct();
    }

    /// <summary>
    /// Checks if a type is a record by looking for compiler-generated members.
    /// Records in C# have an EqualityContract property that is generated by the compiler.
    /// This is the most reliable way to detect records.
    /// </summary>
    private static bool IsRecord(Type type)
    {
        // Records have a protected/public EqualityContract property
        // This is the definitive marker of a record type
        var hasEqualityContract = type.GetProperty("EqualityContract",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public) != null;

        return hasEqualityContract;
    }

    #endregion
}
