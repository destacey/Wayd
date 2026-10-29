using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// Projects an intake desk into its DTO in the database, resolving its members from the shared employee table.
/// </summary>
internal static class IntakeDeskProjections
{
    public static Expression<Func<IntakeDesk, IntakeDeskDto>> Desk(IDemandManagementDbContext dbContext)
    {
        var employees = dbContext.Employees;
        var channels = dbContext.IntakeChannels;
        var tagCategories = dbContext.IntakeRequestTagCategories;
        var scoringModels = dbContext.ScoringModels;

        return d => new IntakeDeskDto
        {
            Id = d.Id,
            Key = d.Key,
            Name = d.Name,
            Description = d.Description,
            Visibility = d.Visibility,
            IsRequesterSelectable = d.IsRequesterSelectable,
            IsOpen = d.IsOpen,
            RequesterMayAddParticipants = d.RequesterMayAddParticipants,
            OffersAllChannels = d.OffersAllChannels,
            ChannelIds = d.Channels
                .Join(channels, dc => dc.ChannelId, c => c.Id, (dc, c) => c)
                .OrderBy(c => c.Order).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => c.Id)
                .ToList(),
            OffersAllTagCategories = d.OffersAllTagCategories,
            AllowsFreeTags = d.AllowsFreeTags,
            TagCategoryIds = d.TagCategories
                .Join(tagCategories, dt => dt.CategoryId, c => c.Id, (dt, c) => c)
                .OrderBy(c => c.Order).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => c.Id)
                .ToList(),
            ScoringModel = scoringModels.Where(m => m.Id == d.ScoringModelId)
                .Select(m => new NavigationDto { Id = m.Id, Key = m.Key, Name = m.Name })
                .FirstOrDefault(),
            Revision = d.Revision,
            Members = d.Members
                .Join(employees, m => m.EmployeeId, e => e.Id, (m, e) => new { m.Role, Employee = e })
                .OrderBy(x => x.Role).ThenBy(x => x.Employee.Name.FirstName).ThenBy(x => x.Employee.Name.LastName).ThenBy(x => x.Employee.Id)
                .Select(x => new IntakeDeskMemberDto
                {
                    Employee = new EmployeeNavigationDto { Id = x.Employee.Id, Key = x.Employee.Key, Name = x.Employee.Name.FirstName + " " + x.Employee.Name.LastName },
                    Role = x.Role,
                })
                .ToList(),
        };
    }
}
