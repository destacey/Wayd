namespace Wayd.Common.Domain.DataProtection;

/// <summary>
/// Marks a string property whose value must be encrypted at rest when its containing
/// object is persisted via an encrypted JSON column.
///
/// Apply to credential-bearing properties on configuration value objects
/// (e.g. <c>PersonalAccessToken</c>, <c>ApiKey</c>). Infrastructure round-trips
/// the value through an <c>ISecretProtector</c> on save/load — domain code
/// continues to see plaintext.
///
/// Lives in the Domain layer (not Infrastructure) so domain types can declare
/// their secret fields without taking an infrastructure dependency.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute
{
}
