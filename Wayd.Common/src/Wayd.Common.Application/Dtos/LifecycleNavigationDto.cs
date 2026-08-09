using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Application.Dtos;

public record LifecycleNavigationDto
{
    public int Id { get; set; }

    [Required]
    public required string Name { get; set; }

    [Required]
    public required string LifecycleCategory { get; set; }

    public static LifecycleNavigationDto FromEnum<T>(T value) where T : struct, Enum
    {
        return new()
        {
            Id = (int)(object)value,
            Name = value.GetDisplayName(),
            LifecycleCategory = value.GetDisplayGroupName() ?? "Unknown"
        };
    }

    public static LifecycleNavigationDto FromEnum<T>(int value) where T : struct, Enum
    {
        return new()
        {
            Id = value,
            Name = ((T)(object)value).GetDisplayName(),
            LifecycleCategory = ((T)(object)value).GetDisplayGroupName() ?? "Unknown"
        };
    }
}
