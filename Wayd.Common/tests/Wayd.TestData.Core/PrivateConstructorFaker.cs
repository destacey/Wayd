using Bogus;

namespace Wayd.TestData.Core;

public class PrivateConstructorFaker<T> : Faker<T> where T : class
{
    public PrivateConstructorFaker() : base("en", IncludePrivateFieldBinder.Create())
    {
        CreateActions[Default] = fakerOfT =>
            this.UsePrivateConstructor() as PrivateConstructorFaker<T>;
    }
}
