using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// The owner types a workflow can be built for, with each one's alias vocabulary.
/// </summary>
/// <remarks>
/// The editor cannot hardcode this: an alias is an <c>int</c> whose meaning belongs to the module that
/// registered it, so "3" is Released for a Release and nothing at all for a Product. Serving the
/// descriptors lets one screen build a workflow for any module that registers one.
/// </remarks>
public sealed record GetWorkflowOwnerTypesQuery : IQuery<List<WorkflowOwnerTypeDto>>;

public sealed class GetWorkflowOwnerTypesQueryHandler
    : IQueryHandler<GetWorkflowOwnerTypesQuery, List<WorkflowOwnerTypeDto>>
{
    public Task<List<WorkflowOwnerTypeDto>> Handle(
        GetWorkflowOwnerTypesQuery request, CancellationToken cancellationToken)
    {
        // Read from the in-memory registry rather than the WorkflowAliasNames table: the registry is
        // whatever this build registered at startup, where the table is a snapshot that a downgrade or
        // a failed seed could leave behind.
        var owners = WorkflowOwners.All
            .Select(descriptor => new WorkflowOwnerTypeDto
            {
                Key = descriptor.Key,
                DisplayName = descriptor.DisplayName,
                RequiredAliases = [.. descriptor.RequiredAliases],
                Aliases =
                [
                    .. descriptor.Aliases
                        .OrderBy(a => a.Key)
                        .Select(a => new WorkflowAliasDto
                        {
                            Value = a.Key,
                            Name = a.Value,
                            IsRequired = descriptor.RequiredAliases.Contains(a.Key),
                        }),
                ],
            })
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(owners);
    }
}
