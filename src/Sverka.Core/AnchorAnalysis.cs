namespace Sverka;

/// <summary>
/// Разбор «якорей» директивы &ИзменениеИКонтроль по образцу трёхстороннего сравнения:
/// тело такого метода расширения содержит копию типового метода с блоками #Вставка и #Удаление.
/// Строки вне #Вставка — это контекст, скопированный из типового кода в момент написания расширения.
/// Если типовой метод после обновления всё ещё содержит эти строки, вставка стоит на прежнем месте
/// и риск ниже. Если строки-якоря пропали, обновление задело именно то место, куда вмешивается расширение.
/// </summary>
public static class AnchorAnalysis
{
    /// <summary>
    /// Итог сравнения якорей вставки с новым телом типового метода.
    /// <c>null</c> — судить нельзя (нет якорных строк или нет тел для сравнения).
    /// </summary>
    public static bool? AnchorIntact(string extensionMethodBody, string? beforeBodyNormalized, string? afterBodyNormalized)
    {
        if (string.IsNullOrEmpty(beforeBodyNormalized) || afterBodyNormalized == null)
            return null;

        var anchors = ExtractAnchorLines(extensionMethodBody);
        if (anchors.Count == 0)
            return null;

        var before = LineSet(beforeBodyNormalized);
        var after = LineSet(afterBodyNormalized);

        var anchoredInBase = anchors.Where(before.Contains).ToList();
        if (anchoredInBase.Count == 0)
            return null;

        return anchoredInBase.All(after.Contains);
    }

    /// <summary>
    /// Нормализованные строки-якоря: контекст типового метода вокруг вставок расширения.
    /// Собственный код внутри #Вставка исключается, строки внутри #Удаление считаются якорями
    /// (это типовые строки, на которые опирается расширение).
    /// </summary>
    public static IReadOnlyList<string> ExtractAnchorLines(string extensionMethodBody)
    {
        var anchors = new List<string>();
        var insideInsert = false;
        foreach (var raw in BslText.Lines(extensionMethodBody))
        {
            var code = BslText.StripComment(raw).Trim();
            if (code.Length == 0)
                continue;

            if (IsDirective(code, "Вставка", "Insert"))
            {
                insideInsert = true;
                continue;
            }

            if (IsDirective(code, "КонецВставки", "EndInsert"))
            {
                insideInsert = false;
                continue;
            }

            if (IsDirective(code, "Удаление", "Delete")
                || IsDirective(code, "КонецУдаления", "EndDelete"))
            {
                continue;
            }

            if (insideInsert)
                continue;

            var normalized = BslText.LowerOutsideStrings(code).Trim();
            if (normalized.Length > 0)
                anchors.Add(normalized);
        }

        return anchors;
    }

    private static bool IsDirective(string code, string russian, string english)
    {
        if (code.Length == 0 || code[0] != '#')
            return false;
        var name = code[1..].TrimStart();
        return name.StartsWith(russian, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(english, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> LineSet(string normalizedBody) =>
        new(normalizedBody.Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
}
