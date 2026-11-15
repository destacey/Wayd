using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Takes out of <c>required</c> a property that only a <c>RuleForEach</c> rule put there.
/// </summary>
/// <remarks>
/// The FluentValidation rules require a property that a <c>NotNull</c> or <c>NotEmpty</c> rule names, and they
/// read <c>RuleForEach(d =&gt; d.Ids).NotEmpty()</c>, which checks each element, as naming the collection. A
/// nullable collection the client may leave out (<c>UpdateIntakeDeskRequest.TriagerIds</c>, where leaving it out
/// keeps the desk's triagers) would then be published as required. Runs straight after those rules, and
/// keeps every property its declaration requires (<see cref="ObjectSchemaTransformer"/>) or an unconditional
/// rule on the property itself requires.
/// </remarks>
public sealed class CollectionRuleRequiredTransformer(ObjectSchemaTransformer declarations) : IOpenApiSchemaTransformer
{
    private readonly ObjectSchemaTransformer _declarations = declarations;

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object || schema.Required is not { Count: > 0 } required)
            return Task.CompletedTask;

        var validators = ValidatorsFor(context);
        if (validators is not { Count: > 0 })
            return Task.CompletedTask;

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            if (!required.Contains(property.Name)
                || _declarations.IsRequiredByDeclaration(property)
                || property.AttributeProvider is not MemberInfo member)
            {
                continue;
            }

            if (!validators.Any(v => RequiresProperty(v, member.Name)))
                required.Remove(property.Name);
        }

        if (required.Count == 0)
            schema.Required = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// The type's validators, or none when they cannot be built: a validator over a <c>DbContext</c> cannot be
    /// in a host without a database. The FluentValidation rules skip such a type too, so it has no rule here to
    /// take back.
    /// </summary>
    private static List<IValidator>? ValidatorsFor(OpenApiSchemaTransformerContext context)
    {
        try
        {
            return context.ApplicationServices.GetService<IValidatorRegistry>()?.GetValidators(context.JsonTypeInfo.Type).ToList();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the validator requires the member itself: an unconditional <c>NotNull</c> or <c>NotEmpty</c> on
    /// a rule for the member, not one applied to each of its elements.
    /// </summary>
    private static bool RequiresProperty(IValidator validator, string member) =>
        validator.CreateDescriptor().GetRulesForMember(member)
            .Where(rule => !IsCollectionRule(rule))
            .SelectMany(rule => rule.Components)
            .Any(component => !component.HasCondition
                && !component.HasAsyncCondition
                && component.Validator is INotNullValidator or INotEmptyValidator);

    private static bool IsCollectionRule(IValidationRule rule) =>
        rule.GetType().GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollectionRule<,>));
}
