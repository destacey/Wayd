namespace Wayd.Common.Extensions;

public static class GuidExtensions
{
    /// <summary>True when the value is the all-zero Guid. <c>default(Guid)</c> and <see cref="Guid.Empty"/>
    /// are the same value, so this covers both.</summary>
    public static bool IsDefault(this Guid value)
        => value == default;

    /// <summary>
    /// True when the value is present AND all-zero. Note a <c>null</c> returns <c>false</c> — this asks
    /// "is this an empty Guid", not "is this missing a usable value". For the latter (the usual intent when
    /// a nullable Guid models "unset"), use <see cref="IsNullEmptyOrDefault"/>.
    /// </summary>
    public static bool IsDefault(this Guid? value)
        => value is not null && value.Value.IsDefault();

    /// <summary>True when the value is <c>null</c> or the all-zero Guid — i.e. carries no usable id.</summary>
    public static bool IsNullEmptyOrDefault(this Guid? value)
        => value is null || value.Value.IsDefault();
}
