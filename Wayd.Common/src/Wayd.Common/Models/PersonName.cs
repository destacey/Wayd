using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Models;

public class PersonName : ValueObject
{
    public PersonName(string firstName, string? middleName, string lastName, string? suffix = null, string? title = null)
    {
        FirstName = Guard.Against.NullOrWhiteSpace(firstName).Trim();
        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
        LastName = Guard.Against.NullOrWhiteSpace(lastName).Trim();
        Suffix = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    public string FirstName { get; } = null!;
    public string? MiddleName { get; }
    public string LastName { get; } = null!;
    public string? Suffix { get; }
    public string? Title { get; }

    public string DisplayName
        => StringHelpers.Concat(FirstName, LastName);

    public string FullName
        => StringHelpers.Concat(Title, FirstName, MiddleName, LastName, Suffix);

    protected override IEnumerable<IComparable?> GetEqualityComponents()
    {
        // Yield every component unconditionally: a conditional yield shifts the ones after it into
        // the wrong slot, so values that differ can compare equal.
        yield return FirstName;
        yield return MiddleName;
        yield return LastName;
        yield return Suffix;
        yield return Title;
    }
}
