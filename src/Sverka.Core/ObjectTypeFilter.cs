namespace Sverka;

/// <summary>
/// Группировка объектов области по типам метаданных для второго дерева-фильтра.
/// </summary>
public static class ObjectTypeFilter
{
    public sealed record TypeGroup(string TypeKey, string Title, IReadOnlyList<string> ObjectNames);

    private static readonly (string Key, string Title)[] KnownTypes =
    [
        ("CommonModule", "Общие модули"),
        ("Constant", "Константы"),
        ("Document", "Документы"),
        ("Report", "Отчёты"),
        ("DataProcessor", "Обработки"),
        ("InformationRegister", "Регистры сведений"),
    ];

    public static IReadOnlyList<TypeGroup> Build(IEnumerable<string> objectFullNames)
    {
        var buckets = KnownTypes.ToDictionary(
            item => item.Key,
            item => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var fullName in objectFullNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var type = TypeOf(fullName);
            if (type != null && buckets.TryGetValue(type, out var list))
                list.Add(fullName);
        }

        return KnownTypes
            .Where(item => buckets[item.Key].Count > 0)
            .Select(item => new TypeGroup(item.Key, item.Title, buckets[item.Key]))
            .ToList();
    }

    public static bool IsKnownType(string fullName) => TypeOf(fullName) != null;

    private static string? TypeOf(string fullName)
    {
        var dot = fullName.IndexOf('.');
        if (dot <= 0)
            return null;
        var type = fullName[..dot];
        return KnownTypes.Any(item => item.Key.Equals(type, StringComparison.OrdinalIgnoreCase))
            ? type
            : null;
    }
}
