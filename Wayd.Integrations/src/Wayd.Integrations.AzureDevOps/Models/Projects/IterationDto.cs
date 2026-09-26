using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Integrations.AzureDevOps.Models.Contracts;
using Wayd.Integrations.AzureDevOps.Utils;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.Projects;

internal sealed record IterationDto
{
    public int Id { get; set; }

    public Guid Identifier { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public Guid? TeamId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool HasChildren { get; set; }

    public static IterationDto FromIterationNodeResponse(IterationNodeResponse node, Guid? teamId = null)
    {
        return new IterationDto
        {
            Id = node.Id,
            Identifier = node.Identifier,
            Name = node.Name,
            Path = ClassificationNodeUtils.RemoveClassificationTypeFromPath(node.Path),
            TeamId = teamId,
            StartDate = node.Attributes?.StartDate,
            EndDate = node.Attributes?.EndDate,
            HasChildren = node.Children is not null && node.Children.Count > 0
        };
    }
}

internal static class IterationDtoExtensions
{
    public static AzdoIteration ToAzdoIteration(this IterationDto iteration, Instant now, Guid projectId)
    {
        AzdoIterationMetadata metadata = new()
        {
            ProjectId = projectId,
            Identifier = iteration.Identifier,
            Path = iteration.Path,
        };

        // Azure DevOps stores an iteration's dates as calendar days, sent as midnight UTC.
        LocalDate? start = iteration.StartDate.HasValue ? Instant.FromDateTimeUtc(iteration.StartDate.Value).InUtc().Date : null;
        LocalDate? end = iteration.EndDate.HasValue ? Instant.FromDateTimeUtc(iteration.EndDate.Value).InUtc().Date : null;

        // Azure DevOps iterations are sprints only if they do not have child iterations and they have start and end dates
        var type = !iteration.HasChildren && start.HasValue && end.HasValue
            ? IterationType.Sprint
            : IterationType.Iteration;

        return new AzdoIteration(iteration.Id, iteration.Name, type, start, end, iteration.TeamId, metadata, now);
    }

    public static List<IExternalIteration<AzdoIterationMetadata>> ToIExternalIterations(this List<IterationDto> iterations, Instant now, Guid projectId)
    {
        List<IExternalIteration<AzdoIterationMetadata>> azdoIterations = new (iterations.Count);
        foreach (var iteration in iterations)
        {
            azdoIterations.Add(iteration.ToAzdoIteration(now, projectId));
        }
        return azdoIterations;
    }
}
