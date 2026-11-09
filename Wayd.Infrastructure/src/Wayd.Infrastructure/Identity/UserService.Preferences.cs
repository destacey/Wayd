using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<UserPreferencesDto> GetPreferences(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        return MapToDto(user.Preferences);
    }

    public async Task<Result> UpdatePreferences(string userId, UserPreferencesDto preferences, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            return Result.Failure("User not found.");
        }

        user.Preferences = WithPreferences(user.Preferences, p =>
        {
            p.Tours = new Dictionary<string, bool>(preferences.Tours);
            p.ThemeConfig = MapThemeConfig(preferences.ThemeConfig);
        });

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Error updating preferences for user {UserId}: {Errors}", userId, errors);
            return Result.Failure(errors);
        }

        return Result.Success();
    }

    public async Task<UserThemeConfigDto?> GetThemeConfig(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        return MapThemeConfigToDto(user.Preferences.ThemeConfig);
    }

    public async Task<Result> UpdateThemeConfig(string userId, UserThemeConfigDto? themeConfig, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            return Result.Failure("User not found.");
        }

        user.Preferences = WithPreferences(user.Preferences, p => p.ThemeConfig = MapThemeConfig(themeConfig));

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Error updating theme config for user {UserId}: {Errors}", userId, errors);
            return Result.Failure(errors);
        }

        return Result.Success();
    }

    public async Task<UserHomeLayoutDto> GetHomeLayout(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        return new UserHomeLayoutDto
        {
            Widgets = user.Preferences?.HomeLayout?.Widgets?
                .Select(w => new UserHomeLayoutWidgetDto { WidgetId = w.WidgetId, Column = w.Column, Visible = w.Visible })
                .ToList() ?? [],
        };
    }

    public async Task<Result> UpdateHomeLayout(string userId, UserHomeLayoutDto layout, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("User with id {UserId} not found.", userId);
            return Result.Failure("User not found.");
        }

        user.Preferences = WithPreferences(user.Preferences, p => p.HomeLayout = layout.Widgets.Count == 0
            ? null
            : new UserHomeLayout
            {
                Widgets = [.. layout.Widgets.Select(w => new UserHomeLayoutWidget { WidgetId = w.WidgetId, Column = w.Column, Visible = w.Visible })],
            });

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Error updating home layout for user {UserId}: {Errors}", userId, errors);
            return Result.Failure(errors);
        }

        return Result.Success();
    }

    /// <summary>
    /// A copy of the user's preferences with one part changed. Every part is carried over, so saving one
    /// never drops another. The copy must be a new instance: EF Core then writes the whole JSON document,
    /// whereas an edit in place patches single keys with a strict JSON_MODIFY, which fails on a stored
    /// document that predates the key (SQL error 13608).
    /// </summary>
    private static UserPreferences WithPreferences(UserPreferences? current, Action<UserPreferences> change)
    {
        var next = new UserPreferences
        {
            Tours = current?.Tours is null ? [] : new Dictionary<string, bool>(current.Tours),
            ThemeConfig = current?.ThemeConfig,
            HomeLayout = current?.HomeLayout,
        };
        change(next);
        return next;
    }

    private static UserThemeConfig? MapThemeConfig(UserThemeConfigDto? themeConfig) =>
        themeConfig is null ? null : new UserThemeConfig
        {
            ColorPrimary = themeConfig.ColorPrimary,
            UseCompactAlgorithm = themeConfig.UseCompactAlgorithm,
        };

    private static UserPreferencesDto MapToDto(UserPreferences preferences) =>
        new()
        {
            Tours = preferences.Tours,
            ThemeConfig = MapThemeConfigToDto(preferences.ThemeConfig),
        };

    private static UserThemeConfigDto? MapThemeConfigToDto(UserThemeConfig? themeConfig) =>
        themeConfig is null ? null : new UserThemeConfigDto
        {
            ColorPrimary = themeConfig.ColorPrimary,
            UseCompactAlgorithm = themeConfig.UseCompactAlgorithm,
        };
}
