using System.Linq.Expressions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// Projects an intake desk into its DTO in the database, resolving its reviewers from the shared employee table.
/// </summary>
internal static class IntakeDeskProjections
{
    public static Expression<Func<IntakeDesk, IntakeDeskDto>> Desk(IDemandManagementDbContext dbContext)
    {
        var employees = dbContext.Employees;

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
            Reviewers = employees
                .Where(e => d.Reviewers.Any(r => r.EmployeeId == e.Id))
                .OrderBy(e => e.Name.FirstName).ThenBy(e => e.Name.LastName).ThenBy(e => e.Id)
                .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                .ToList(),
        };
    }
}
