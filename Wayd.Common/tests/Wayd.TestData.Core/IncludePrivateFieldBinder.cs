using System.Reflection;

namespace Wayd.TestData.Core;

public class IncludePrivateFieldBinder : Bogus.Binder
{
    public override Dictionary<string, MemberInfo> GetMembers(Type type)
    {
        var members = base.GetMembers(type);

        var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        var allMembers = type.GetMembers(bindingFlags);
        var fields = allMembers
            .OfType<FieldInfo>()
            .ToList();

        foreach (var field in fields)
        {
            var fieldName = field.Name;
            if (fieldName.StartsWith("<") && fieldName.EndsWith(">k__BackingField"))
                fieldName = fieldName[1..].Replace(">k__BackingField", "");

            if (!members.ContainsKey(fieldName))
                members.Add(fieldName, field);
        }

        return members;
    }

    public static IncludePrivateFieldBinder Create()
    {
        return new IncludePrivateFieldBinder();
    }
}
