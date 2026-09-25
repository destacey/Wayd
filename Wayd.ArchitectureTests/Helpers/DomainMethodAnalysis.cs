using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Wayd.ArchitectureTests.Helpers;

/// <summary>
/// Reads the compiled domain assemblies' IL to answer two questions about a method, following every call it
/// makes into domain code: can it change an entity's state, and can it raise a domain event?
/// </summary>
/// <remarks>
/// Both answers are "somewhere on some path", not "on every path". A method that raises in one branch and
/// changes state unevented in another is not caught; the guard is built to catch a mutator with no event at
/// all, without flagging anything that has one.
/// </remarks>
public sealed class DomainMethodAnalysis
{
    private const string CommonDomainAssembly = "Wayd.Common.Domain";
    private const string BaseEntityFullName = "Wayd.Common.Domain.Data.BaseEntity`1";
    private const string DomainEventFullName = "Wayd.Common.Domain.Events.DomainEvent";

    private static readonly HashSet<string> CollectionMutators =
    [
        "Add", "AddRange", "Insert", "InsertRange", "TryAdd", "set_Item",
        "Remove", "RemoveAll", "RemoveAt", "RemoveRange", "RemoveWhere", "Clear",
        "UnionWith", "ExceptWith", "IntersectWith", "SymmetricExceptWith", "Sort", "Reverse",
    ];

    private readonly Dictionary<string, ModuleDefinition> _modules;
    private readonly Dictionary<MethodDefinition, Summary> _summaries = [];
    private readonly HashSet<MethodDefinition> _inProgress = [];

    // The memo and the cycle guard are shared by every caller, and a test host may run them concurrently.
    private readonly Lock _gate = new();

    private DomainMethodAnalysis(Dictionary<string, ModuleDefinition> modules) => _modules = modules;

    public static DomainMethodAnalysis Load()
    {
        var directory = AssemblyHelper.GetAssemblyDirectory();
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(directory);

        // In memory: the analysis lives as long as the test host, and a module read from its path holds the
        // file open, which fails the next build's copy of it while an IDE keeps the host running.
        var parameters = new ReaderParameters { AssemblyResolver = resolver, InMemory = true };
        var modules = AssemblyHelper.GetDomainAssemblies()
            .Select(a => ModuleDefinition.ReadModule(a.Location, parameters))
            .ToDictionary(m => m.Assembly.Name.Name);

        return new DomainMethodAnalysis(modules);
    }

    public TypeDefinition Definition(Type type) =>
        _modules[type.Assembly.GetName().Name!].GetType(type.FullName!.Replace('+', '/'))
        ?? throw new InvalidOperationException($"{type.FullName} is not in the domain assemblies Cecil loaded.");

    /// <summary>Whether this method, or anything it calls in domain code, calls <c>AddDomainEvent</c>.</summary>
    public bool Raises(MethodDefinition method)
    {
        lock (_gate)
            return Summarize(method).Raises;
    }

    /// <summary>Whether this method, or anything it calls in domain code, writes an entity's state.</summary>
    public bool ChangesState(MethodDefinition method)
    {
        lock (_gate)
            return Summarize(method).ChangesState;
    }

    /// <summary>Whether this method's own body calls <c>AddDomainEvent</c>, not counting what it calls.</summary>
    public static bool RaisesDirectly(MethodDefinition method) =>
        method.HasBody && method.Body.Instructions.Any(i => IsCall(i) && IsAddDomainEvent((MethodReference)i.Operand));

    /// <summary>Every type in the domain assemblies, nested ones included.</summary>
    public IEnumerable<TypeDefinition> DomainTypes() => _modules.Values.SelectMany(m => m.GetTypes());

    /// <summary>
    /// The type a method was written in: a lambda compiles into a closure class nested in it, and the raise
    /// inside belongs to the enclosing type.
    /// </summary>
    public static TypeDefinition Owner(TypeDefinition type)
    {
        var current = type;
        while (current.DeclaringType is not null && IsCompilerGenerated(current))
            current = current.DeclaringType;

        return current;
    }

    public static bool DerivesFrom(TypeDefinition type, TypeDefinition ancestor)
    {
        for (var current = type; current is not null; current = current.BaseType?.Resolve())
        {
            if (current.FullName == ancestor.FullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The common entity base classes: identity, audit columns, soft delete and the event queue itself. Their
    /// members are how every entity is persisted, not the aggregate's own behaviour.
    /// </summary>
    public static bool IsEntityPlumbing(TypeDefinition type) => type.Namespace == "Wayd.Common.Domain.Data";

    private static bool IsEntity(TypeDefinition? type)
    {
        for (var current = type; current is not null; current = current.BaseType?.Resolve())
        {
            if (current.FullName == BaseEntityFullName)
                return true;
        }

        return false;
    }

    private Summary Summarize(MethodDefinition method)
    {
        if (_summaries.TryGetValue(method, out var known))
            return known;

        // A cycle contributes nothing new: whatever the method does is found on the path that entered it.
        if (!method.HasBody || !_inProgress.Add(method))
            return default;

        var changesState = false;
        var raises = false;
        var fieldTypesRead = method.Body.Instructions
            .Where(i => i.OpCode.Code is Code.Ldfld or Code.Ldflda)
            .Select(i => (FieldReference)i.Operand)
            .Where(f => IsStatefulDomainType(f.DeclaringType.Resolve()))
            .Select(f => f.FieldType.FullName)
            .ToHashSet();

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code is Code.Stfld or Code.Stsfld)
            {
                changesState |= IsStatefulDomainType(((FieldReference)instruction.Operand).DeclaringType.Resolve());
                continue;
            }

            if (instruction.Operand is not MethodReference callee)
                continue;

            if (IsAddDomainEvent(callee))
            {
                raises = true;
                continue;
            }

            changesState |= IsCall(instruction) && IsSetter(callee) && !IsInitOnly(callee)
                && IsStatefulDomainType(callee.DeclaringType.Resolve());
            changesState |= IsCall(instruction) && IsCollectionMutator(callee)
                && fieldTypesRead.Contains(callee.DeclaringType.FullName);

            var target = Follow(instruction, callee);
            if (target is not null)
            {
                var inner = Summarize(target);
                changesState |= inner.ChangesState;
                raises |= inner.Raises;
            }
        }

        _inProgress.Remove(method);
        var summary = new Summary(changesState, raises);
        _summaries[method] = summary;
        return summary;
    }

    /// <summary>
    /// The domain method a call, constructor or delegate leads into, or null when it leaves domain code.
    /// A constructor is followed only for an entity: building an event or a value object changes nothing, and
    /// what it is assigned to is caught at the assignment.
    /// </summary>
    private MethodDefinition? Follow(Instruction instruction, MethodReference callee)
    {
        if (instruction.OpCode.Code is not (Code.Call or Code.Callvirt or Code.Newobj or Code.Ldftn or Code.Ldvirtftn))
            return null;
        if (!_modules.ContainsKey(ScopeName(callee)))
            return null;

        var definition = callee.Resolve();
        if (definition is null)
            return null;
        if (instruction.OpCode.Code == Code.Newobj && !IsEntity(definition.DeclaringType))
            return null;

        return definition;
    }

    private static string ScopeName(MethodReference callee) => callee.DeclaringType.Scope switch
    {
        AssemblyNameReference reference => reference.Name,
        ModuleDefinition module => module.Assembly.Name.Name,
        var other => other.Name,
    };

    /// <summary>
    /// A class of the domain's own whose fields are state: entities, and the helpers they keep that state in
    /// (a role manager, say). Events and compiler-generated closures are excluded — writing one of those
    /// records or captures a value, it does not change a record.
    /// </summary>
    private bool IsStatefulDomainType(TypeDefinition? type)
    {
        if (type is null || !_modules.ContainsKey(type.Module.Assembly.Name.Name))
            return false;
        if (type.IsValueType || IsCompilerGenerated(type))
            return false;

        for (var current = type; current is not null; current = current.BaseType?.Resolve())
        {
            if (current.FullName == DomainEventFullName)
                return false;
        }

        return true;
    }

    private static bool IsCompilerGenerated(TypeDefinition type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.Name.Contains('<')
                || current.CustomAttributes.Any(a => a.AttributeType.Name == "CompilerGeneratedAttribute"))
                return true;
        }

        return false;
    }

    private static bool IsCall(Instruction instruction) => instruction.OpCode.Code is Code.Call or Code.Callvirt;

    private static bool IsAddDomainEvent(MethodReference method) =>
        method.Name == "AddDomainEvent" && ScopeName(method) == CommonDomainAssembly;

    private static bool IsSetter(MethodReference method) => method.Name.StartsWith("set_", StringComparison.Ordinal);

    private static bool IsInitOnly(MethodReference method) =>
        method.ReturnType is RequiredModifierType { ModifierType.Name: "IsExternalInit" };

    private static bool IsCollectionMutator(MethodReference method) =>
        CollectionMutators.Contains(method.Name)
        && method.DeclaringType.Namespace.StartsWith("System.Collections", StringComparison.Ordinal);

    private readonly record struct Summary(bool ChangesState, bool Raises);
}
