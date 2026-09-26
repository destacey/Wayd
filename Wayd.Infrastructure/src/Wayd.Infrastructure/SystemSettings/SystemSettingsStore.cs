using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.SystemSettings;

internal sealed class SystemSettingsStore(
    WaydDbContext dbContext,
    IMemoryCache cache,
    IServiceProvider serviceProvider,
    IDateTimeProvider dateTimeProvider,
    ILogger<SystemSettingsStore> logger) : ISystemSettingsStore
{
    // A save clears only this instance's cache, so another API instance serves the old values for at most
    // this long.
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    private readonly WaydDbContext _dbContext = dbContext;
    private readonly IMemoryCache _cache = cache;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<SystemSettingsStore> _logger = logger;

    public async Task<TSection> Get<TSection>(CancellationToken cancellationToken)
        where TSection : class, ISettingsSection<TSection>, new()
    {
        if (_cache.TryGetValue<TSection>(CacheKey<TSection>(), out var cached) && cached is not null)
            return cached;

        var id = SystemSettingsSection.IdFor<TSection>();
        var stored = await _dbContext.SystemSettingsSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        var values = SystemSettingsSection.Read<TSection>(stored);

        _cache.Set(CacheKey<TSection>(), values, _cacheExpiration);

        return values;
    }

    public async Task<Result> Save<TSection>(TSection values, EventActor actor, CancellationToken cancellationToken)
        where TSection : class, ISettingsSection<TSection>, new()
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var validator in _serviceProvider.GetServices<IValidator<TSection>>())
        {
            var validation = await validator.ValidateAsync(values, cancellationToken);
            if (!validation.IsValid)
                return Result.Failure(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var id = SystemSettingsSection.IdFor<TSection>();
        var stored = await _dbContext.SystemSettingsSections
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        var timestamp = _dateTimeProvider.Now;
        if (stored is null)
        {
            var created = SystemSettingsSection.Create(values, actor, timestamp);
            if (created is null)
                return Result.Success();

            _dbContext.SystemSettingsSections.Add(created);
        }
        else if (!stored.Change(values, actor, timestamp))
        {
            return Result.Success();
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || ex.IsUniqueViolation())
        {
            // The id is derived from the key, so two first saves racing collide on it rather than both inserting.
            _logger.LogWarning(ex, "Saving the {SettingsSection} settings conflicted with another save.", TSection.Key);
            _dbContext.ChangeTracker.Clear();
            return Result.Failure("These settings were changed by someone else while you were editing. Reload them and try again.");
        }
        finally
        {
            _cache.Remove(CacheKey<TSection>());
        }

        _logger.LogInformation("System settings section {SettingsSection} saved.", TSection.Key);

        return Result.Success();
    }

    private static string CacheKey<TSection>()
        where TSection : class, ISettingsSection<TSection>, new() =>
        $"SystemSettings:{TSection.Key}";
}
