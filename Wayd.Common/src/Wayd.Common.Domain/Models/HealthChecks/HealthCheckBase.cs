using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Extensions;
using NodaTime;

namespace Wayd.Common.Domain.Models.HealthChecks;

public abstract class HealthCheckBase : BaseSoftDeletableEntity
{
    protected HealthCheckBase() { }

    protected HealthCheckBase(HealthStatus status, Guid reportedById, Instant reportedOn, Instant expiration, string? note)
    {
        if (expiration <= reportedOn)
            throw new ArgumentException("Expiration must be greater than ReportedOn.", nameof(expiration));

        Status = status;
        ReportedById = reportedById;
        ReportedOn = reportedOn;
        Expiration = expiration;
        Note = note;
    }

    public HealthStatus Status { get; private set; }

    public Guid ReportedById { get; private init; }

    public Employee ReportedBy { get; private set; } = default!;

    public Instant ReportedOn { get; private init; }

    public Instant Expiration { get; private set; }

    public string? Note
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    public bool IsExpired(Instant now) => Expiration <= now;

    public void ChangeExpiration(Instant expiration)
    {
        if (expiration < ReportedOn)
            throw new ArgumentException("Expiration must be greater than or equal to ReportedOn.", nameof(expiration));

        Expiration = expiration;
    }

    public Result Update(HealthStatus status, Instant expiration, string? note, Instant now)
    {
        if (IsExpired(now))
            return Result.Failure("Expired health checks cannot be modified.");

        if (expiration <= now)
            return Result.Failure("Expiration must be in the future.");

        Status = status;
        Expiration = expiration;
        Note = note;
        return Result.Success();
    }
}
