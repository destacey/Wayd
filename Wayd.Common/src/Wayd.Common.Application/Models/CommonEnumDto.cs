using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Application.Models;

public record CommonEnumDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "Unknown Name";
    public string? Description { get; set; }
    public int Order { get; set; }

    public static List<TType> GetValues<TEnum, TType>() where TEnum : struct, Enum where TType : CommonEnumDto, new()
    {
        return Enum.GetValues<TEnum>().Select(v => new TType
        {
            Id = (int)(object)v,
            Name = v.GetDisplayName(),
            Description = v.GetDisplayDescription(),
            Order = v.GetDisplayOrder()
        }).ToList();
    }
}

/// <summary>
/// One value of a closed set of options, carrying the stable <see cref="Code"/> that endpoints
/// filtering on the set accept.
/// </summary>
/// <remarks>
/// Callers filter by the code rather than <see cref="CommonEnumDto.Id"/>, so the set can be
/// reordered or renumbered without breaking them.
/// </remarks>
public record CommonEnumDto<TEnum> : CommonEnumDto where TEnum : struct, Enum
{
    /// <summary>The value's stable name, as filters on this set accept it.</summary>
    /// <remarks>
    /// <c>[Required]</c> because NSwag does not infer it for a value-type property declared on a generic
    /// base; without it the generated clients type the code as optional.
    /// </remarks>
    [Required]
    public TEnum Code { get; set; }

    /// <summary>Lists every value of <typeparamref name="TEnum"/> with its display metadata.</summary>
    public static List<TType> GetValues<TType>() where TType : CommonEnumDto<TEnum>, new()
    {
        return Enum.GetValues<TEnum>().Select(v => new TType
        {
            Id = (int)(object)v,
            Code = v,
            Name = v.GetDisplayName(),
            Description = v.GetDisplayDescription(),
            Order = v.GetDisplayOrder()
        }).ToList();
    }
}