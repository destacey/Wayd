using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Domain.Models;

namespace Wayd.Web.Api.Models.Organizations.Employees;

public sealed record ChangeEmployeeAllocationsRequest
{
    /// <summary>
    /// The changes to make, applied together or not at all. Each sets what one team and role have over its date
    /// range and leaves the days outside it as they were.
    /// </summary>
    public List<EmployeeAllocationChangeRequest> Changes { get; set; } = [];

    public ChangeEmployeeAllocationsCommand ToChangeEmployeeAllocationsCommand(Guid employeeId) =>
        new(employeeId, [.. Changes.Select(c => new AllocationChange(c.TeamId, c.RoleId, c.Start, c.End, c.Percent))]);
}

public sealed record EmployeeAllocationChangeRequest
{
    /// <summary>The team or team of teams. The employee need not be a member of it.</summary>
    public Guid TeamId { get; set; }

    /// <summary>The team member role the time is spent in, or null for none.</summary>
    public Guid? RoleId { get; set; }

    /// <summary>The first day the change covers.</summary>
    public LocalDate Start { get; set; }

    /// <summary>The last day the change covers, or null for every day from the start on.</summary>
    public LocalDate? End { get; set; }

    /// <summary>The share of the employee's time, from 0 to 100. Zero ends the allocation for the date range.</summary>
    public int Percent { get; set; }
}

public sealed class ChangeEmployeeAllocationsRequestValidator : CustomValidator<ChangeEmployeeAllocationsRequest>
{
    public ChangeEmployeeAllocationsRequestValidator()
    {
        RuleFor(r => r.Changes)
            .NotEmpty();

        RuleForEach(r => r.Changes).ChildRules(change =>
        {
            change.RuleFor(c => c.TeamId)
                .NotEmpty();

            change.RuleFor(c => c.Percent)
                .InclusiveBetween(0, EmployeeCapacity.FullAllocation);

            change.RuleFor(c => c.End)
                .GreaterThanOrEqualTo(c => c.Start)
                .When(c => c.End.HasValue)
                .WithMessage("An allocation cannot end before it starts.");
        });
    }
}

public sealed record MoveEmployeeAllocationRequest
{
    /// <summary>The team or team of teams the time is taken from.</summary>
    public Guid FromTeamId { get; set; }

    /// <summary>The role the time is taken from on that team, or null for the allocation with no role.</summary>
    public Guid? FromRoleId { get; set; }

    /// <summary>The team or team of teams the time is given to. It can be the same team, in another role.</summary>
    public Guid ToTeamId { get; set; }

    /// <summary>The role the time is given to on that team, or null for none.</summary>
    public Guid? ToRoleId { get; set; }

    /// <summary>The first day of the move.</summary>
    public LocalDate Start { get; set; }

    /// <summary>The last day of the move, or null for every day from the start on.</summary>
    public LocalDate? End { get; set; }

    /// <summary>The share of the employee's time to move, from 1 to 100.</summary>
    public int Percent { get; set; }

    public MoveEmployeeAllocationCommand ToMoveEmployeeAllocationCommand(Guid employeeId) =>
        new(employeeId, FromTeamId, FromRoleId, ToTeamId, ToRoleId, Start, End, Percent);
}

public sealed class MoveEmployeeAllocationRequestValidator : CustomValidator<MoveEmployeeAllocationRequest>
{
    public MoveEmployeeAllocationRequestValidator()
    {
        RuleFor(r => r.FromTeamId)
            .NotEmpty();

        RuleFor(r => r.ToTeamId)
            .NotEmpty();

        RuleFor(r => r.Percent)
            .InclusiveBetween(1, EmployeeCapacity.FullAllocation);

        RuleFor(r => r.End)
            .GreaterThanOrEqualTo(r => r.Start)
            .When(r => r.End.HasValue)
            .WithMessage("An allocation cannot end before it starts.");
    }
}
