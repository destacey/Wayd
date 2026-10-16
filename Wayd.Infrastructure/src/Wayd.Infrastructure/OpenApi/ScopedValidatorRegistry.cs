using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wayd.Infrastructure.OpenApi;

/// <summary>
/// Finds the validators the OpenAPI document reads its rules from, each lookup in a scope of its own.
/// </summary>
/// <remarks>
/// <c>IOpenApiDocumentProvider</c> generates the document from the root provider, outside any request — for
/// the MCP server and for the build — and validators are scoped, some over a <c>DbContext</c>. A scope per
/// lookup keeps those dependencies from living as long as the application. The validator outlives its scope,
/// which is safe only because the document reads its rules and never runs it.
/// </remarks>
internal sealed class ScopedValidatorRegistry(IServiceScopeFactory scopes, IOptions<SchemaGenerationOptions> options) : IValidatorRegistry
{
    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly IOptions<SchemaGenerationOptions> _options = options;

    /// <inheritdoc />
    public IValidator? GetValidator(Type type)
    {
        using var scope = _scopes.CreateScope();
        return new ServiceProviderValidatorRegistry(scope.ServiceProvider, _options).GetValidator(type);
    }

    /// <inheritdoc />
    public IEnumerable<IValidator> GetValidators(Type type)
    {
        using var scope = _scopes.CreateScope();
        return [.. new ServiceProviderValidatorRegistry(scope.ServiceProvider, _options).GetValidators(type)];
    }
}
