using CSharpFunctionalExtensions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Commands;
using Wayd.ProductManagement.Application.Incidents.Queries;

namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Records incident timeline entries when generic links change on an incident object.
/// </summary>
internal static class IncidentTimelineLinkRecording
{
    internal static async Task<Result> RecordGenericLinkChangeAsync(
        IDispatcher dispatcher,
        Guid objectId,
        IncidentTimelineLinkAction action,
        string label,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Success();
        }

        var incident = await dispatcher.Send(new GetIncidentQuery(new IdOrKey(objectId)), cancellationToken);
        if (incident is null)
        {
            return Result.Success();
        }

        return await dispatcher.Send(
            new RecordIncidentTimelineLinkEntriesCommand(
                incident.Id,
                [
                    new IncidentTimelineLinkChangeDto(
                        IncidentTimelineLinkKind.Generic,
                        action,
                        label.Trim()),
                ]),
            cancellationToken);
    }
}
