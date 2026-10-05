using System.Linq.Expressions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using OneOf;

namespace Wayd.Common.Application.Models.Organizations;

public sealed class TeamIdOrCode : OneOfBase<Guid, TeamCode>
{
    public TeamIdOrCode(OneOf<Guid, TeamCode> value) : base(value) { }

    /// <summary>Reads a route or query value as a Guid id or a team code.</summary>
    /// <exception cref="NotFoundException">The value is neither, so it can name no team.</exception>
    public TeamIdOrCode(string value) : base(Parse(value)) { }

    private static OneOf<Guid, TeamCode> Parse(string value)
    {
        if (Guid.TryParse(value, out var guid))
            return guid;

        try
        {
            return new TeamCode(value);
        }
        catch (ArgumentException)
        {
            throw new NotFoundException($"No team has the id or code '{value}'.");
        }
    }

    public static implicit operator TeamIdOrCode(Guid value) => new(OneOf<Guid, TeamCode>.FromT0(value));
    public static implicit operator TeamIdOrCode(TeamCode value) => new(OneOf<Guid, TeamCode>.FromT1(value));
    public static implicit operator TeamIdOrCode(string value) => new(value);
}

public static class TeamIdOrCodeExtensions
{
    /// <summary>
    /// Creates an expression based on the values from the TeamIdOrCode for objects that implement IHasTeamIdAndCode.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="idOrCode"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> CreateFilter<T>(this TeamIdOrCode idOrCode)
        where T : IHasTeamIdAndCode
    {
        return idOrCode.Match<Expression<Func<T, bool>>>(
            id => x => x.Id == id,
            code => x => x.Code == code
        );
    }
}
