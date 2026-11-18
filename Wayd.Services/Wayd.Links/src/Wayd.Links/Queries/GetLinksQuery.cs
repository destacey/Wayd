using CSharpFunctionalExtensions;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Validation;
using Wayd.Common.Domain.Links;
using Wayd.Links.Models;

namespace Wayd.Links.Queries;

/// <summary>
/// A record's links and whether the caller may change them. Fails when the owner type is not registered; null when
/// the record does not exist or the caller cannot read it.
/// </summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
public sealed record GetLinksQuery(string OwnerType, Guid OwnerId) : IQuery<Result<RecordLinksDto?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetLinksQueryValidator : CustomValidator<GetLinksQuery>
{
    public GetLinksQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(LinkOwnerTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
    }
}

/// <summary>Reads the links once the record's owner type lets the caller read the record.</summary>
public sealed class GetLinksQueryHandler(LinkOwnerAccessResolver accessResolver, ILinksDbContext linksDbContext)
    : IQueryHandler<GetLinksQuery, Result<RecordLinksDto?>>
{
    private readonly LinkOwnerAccessResolver _accessResolver = accessResolver;
    private readonly ILinksDbContext _linksDbContext = linksDbContext;

    public async Task<Result<RecordLinksDto?>> Handle(GetLinksQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.OwnerType, request.OwnerId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<RecordLinksDto?>(resolved.Error);

        if (resolved.Value is not { } owner)
            return Result.Success<RecordLinksDto?>(null);

        var links = await _linksDbContext.Links
            .Where(l => l.OwnerType == owner.OwnerType && l.OwnerId == owner.OwnerId)
            .OrderBy(l => l.Name)
            .ThenBy(l => l.Id)
            .ProjectToType<LinkDto>()
            .ToListAsync(cancellationToken);

        return new RecordLinksDto { CanManage = owner.CanManage, Links = links };
    }
}
