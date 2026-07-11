using System.Reflection;
using Bogus;

namespace Wayd.TestData.Core;

public static class FakerExtensions
{
    public static Faker<T> UsePrivateConstructor<T>(this Faker<T> faker) where T : class
    {
        return faker.CustomInstantiator(f =>
            Activator.CreateInstance(typeof(T), nonPublic: true) as T
            ?? throw new ArgumentNullException($"typeof({typeof(T)}) does not have a private constructor."));
    }

    public static Faker<T> RuleForPrivate<T, TProperty>(this Faker<T> faker, string propertyName, Func<Faker, TProperty> setter) where T : class
    {
        return faker.RuleFor(propertyName, setter);
    }

    public static Faker<T> FinishWith<T>(this Faker<T> faker, string methodName, params object[] parameters) where T : class
    {
        return faker.FinishWith((f, instance) =>
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type {typeof(T).Name}");
            }
            method.Invoke(instance, parameters);
        });
    }
}
