namespace Wayd.Common.Extensions;

public static class GuidExtensions
{
    public static bool IsDefault(this Guid value)
        => value == default;

    /// <summary>
    /// True only when the value is present AND all-zero — a <c>null</c> returns <c>false</c>. For the usual
    /// "carries no usable id" question, use <see cref="IsNullEmptyOrDefault"/> instead.
    /// </summary>
    public static bool IsDefault(this Guid? value)
        => value is not null && value.Value.IsDefault();

    public static bool IsNullEmptyOrDefault(this Guid? value)
        => value is null || value.Value.IsDefault();
}
