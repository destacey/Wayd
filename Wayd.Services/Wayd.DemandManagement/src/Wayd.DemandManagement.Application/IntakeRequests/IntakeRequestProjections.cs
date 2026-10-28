using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeChannels.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Projects an intake request into each tier's DTO in the database.
/// </summary>
/// <remarks>
/// One projection per tier, so a query that may only return the summary has no path to a substance field.
/// The people a request points at are employees in the shared table, resolved here by id because the
/// request holds ids rather than navigations into another module's data.
/// </remarks>
internal static class IntakeRequestProjections
{
    public static Expression<Func<IntakeRequest, IntakeRequestSummaryDto>> Summary(IDemandManagementDbContext dbContext)
    {
        var desks = dbContext.IntakeDesks;
        var channels = dbContext.IntakeChannels;
        var employees = dbContext.Employees;
        var transitions = dbContext.IntakeRequestStatusTransitions;
        var tagCategories = dbContext.IntakeRequestTagCategories;

        // Every request records its initial status as its first transition, so both reads always find a row.
        return r => new IntakeRequestSummaryDto
        {
            Id = r.Id,
            Key = r.Key,
            Title = r.Title,
            Status = new StatusNavigationDto { Id = r.StatusId, Name = r.StatusName, Category = r.StatusCategory, Alias = r.StatusAliasValue },
            Desk = desks.Where(d => d.Id == r.DeskId)
                .Select(d => new NavigationDto { Id = d.Id, Key = d.Key, Name = d.Name })
                .First(),
            Requester = employees.Where(e => e.Id == r.RequesterId)
                .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                .First(),
            Channel = channels.Where(c => c.Id == r.ChannelId)
                .Select(c => new IntakeChannelNavigationDto { Id = c.Id, Name = c.Name })
                .FirstOrDefault(),
            Assignee = employees.Where(e => e.Id == r.AssigneeId)
                .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                .FirstOrDefault(),
            NeedBy = r.NeedBy == null
                ? null
                : new IntakeRequestNeedByDto
                {
                    Date = r.NeedBy.Date,
                    Basis = r.NeedBy.Basis,
                    Reference = r.NeedBy.Reference,
                    CommittedBy = employees.Where(e => e.Id == r.NeedBy.CommittedById)
                        .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                        .FirstOrDefault(),
                    CommittedOn = r.NeedBy.CommittedOn,
                },
            Tags = tagCategories
                .SelectMany(c => c.Tags, (c, t) => new { Category = c, Tag = t })
                .Where(x => r.Tags.Any(a => a.TagId == x.Tag.Id))
                .OrderBy(x => x.Category.Order).ThenBy(x => x.Category.Name).ThenBy(x => x.Tag.Name).ThenBy(x => x.Tag.Id)
                .Select(x => new IntakeRequestTagNavigationDto
                {
                    Id = x.Tag.Id,
                    Name = x.Tag.Name,
                    CategoryId = x.Category.Id,
                    CategoryName = x.Category.Name,
                })
                .ToList(),
            SubmittedOn = transitions.Where(t => t.RecordId == r.Id)
                .OrderBy(t => t.Sequence)
                .Select(t => t.ChangedOn)
                .First(),
            StatusChangedOn = transitions.Where(t => t.RecordId == r.Id)
                .OrderByDescending(t => t.Sequence)
                .Select(t => t.ChangedOn)
                .First(),
        };
    }

    public static Expression<Func<IntakeRequest, IntakeRequestSubstanceDto>> Substance(IDemandManagementDbContext dbContext)
    {
        var employees = dbContext.Employees;

        return r => new IntakeRequestSubstanceDto
        {
            Description = r.Description,
            ExpectedBenefits = r.ExpectedBenefits,
            SubmittedBy = employees.Where(e => e.Id == r.SubmittedById)
                .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                .First(),
            Participants = r.Participants
                .OrderBy(p => p.AddedOn).ThenBy(p => p.EmployeeId)
                .Select(p => new IntakeRequestParticipantDto
                {
                    Employee = employees.Where(e => e.Id == p.EmployeeId)
                        .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                        .First(),
                    AddedBy = employees.Where(e => e.Id == p.AddedById)
                        .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                        .FirstOrDefault(),
                    AddedOn = p.AddedOn,
                })
                .ToList(),
            Origin = r.Origin == null
                ? null
                : new IntakeRequestOriginDto
                {
                    SourceSystem = r.Origin.SourceSystem,
                    ExternalId = r.Origin.ExternalId,
                    Url = r.Origin.Url,
                },
            StatusReason = r.StatusReason,
        };
    }
}
