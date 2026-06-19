using Wayd.Links.Models;
using Wayd.Tests.Shared.Data;

namespace Wayd.Links.Tests.Data;

public sealed class LinkFaker : PrivateConstructorFaker<Link>
{
    public LinkFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.ObjectId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.Url, f => f.Internet.Url());
    }
}

public static class LinkFakerExtensions
{
    public static LinkFaker WithId(this LinkFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static LinkFaker WithObjectId(this LinkFaker faker, Guid objectId)
    {
        faker.RuleFor(x => x.ObjectId, objectId);
        return faker;
    }

    public static LinkFaker WithName(this LinkFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static LinkFaker WithUrl(this LinkFaker faker, string url)
    {
        faker.RuleFor(x => x.Url, url);
        return faker;
    }
}
