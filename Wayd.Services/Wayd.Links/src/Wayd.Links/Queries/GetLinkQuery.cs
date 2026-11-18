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
/// One of a record's links. Fails when the owner type is not registered; null when the record or the link does not
/// exist, the link is on another record, or the caller cannot read the record.
/// </summary>
/// <param name="OwnerType">The record's registered owner type.</param>
/// <param name="OwnerId">The record's id.</param>
/// <param name="LinkId">The link's id.</param>
public sealed record GetLinkQuery(string OwnerType, Guid OwnerId, Guid LinkId) : IQuery<Result<LinkDto?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetLinkQueryValidator : CustomValidator<GetLinkQuery>
{
    public GetLinkQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(LinkOwnerTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
        RuleFor(q => q.LinkId).NotEmpty();
    }
}

/// <summary>Reads the link once the record's owner type lets the caller read the record.</summary>
public sealed class GetLinkQueryHandler(LinkOwnerAccessResolver accessResolver, ILinksDbContext linksDbContext)
    : IQueryHandler<GetLinkQuery, Result<LinkDto?>>
{
    private readonly LinkOwnerAccessResolver _accessResolver = accessResolver;
    private readonly ILinksDbContext _linksDbContext = linksDbContext;

    public async Task<Result<LinkDto?>> Handle(GetLinkQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.OwnerType, request.OwnerId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<LinkDto?>(resolved.Error);

        if (resolved.Value is not { } owner)
            return Result.Success<LinkDto?>(null);

        var link = await _linksDbContext.Links
            .Where(l => l.Id == request.LinkId && l.OwnerType == owner.OwnerType && l.OwnerId == owner.OwnerId)
            .ProjectToType<LinkDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(link);
    }
}
