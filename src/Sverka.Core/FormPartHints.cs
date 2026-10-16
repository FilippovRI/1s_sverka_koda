using System.Text.RegularExpressions;

namespace Sverka;

/// <summary>Подсказки для скелета формы из отпечатка члена Form.xml.</summary>
public static class FormPartHints
{
    private static readonly Regex TypeBlock = new(
        @"Type\{(?<body>[^}]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? ExtractTypeDescription(string? fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return null;
        var match = TypeBlock.Match(fingerprint);
        if (!match.Success)
            return null;
        var body = match.Groups["body"].Value.Trim();
        if (body.Length == 0)
            return null;
        // Canonical кладёт текст типа как содержимое; иногда с префиксами.
        body = body.Split(';')[0].Trim();
        return body.Length == 0 ? null : body;
    }

    public static string? ExtractElementKind(string? fingerprint)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return null;
        var brace = fingerprint.IndexOf('{');
        if (brace <= 0)
            return null;
        return fingerprint[..brace];
    }

    public static string MapElementType(string? xmlKind) => xmlKind?.ToLowerInvariant() switch
    {
        "button" or "usualbutton" or "buttonform" => "КнопкаФормы",
        "labeldecoration" or "label" or "labelfield" => "ДекорацияФормы",
        "table" or "formtable" => "ТаблицаФормы",
        "pages" or "page" or "group" or "usualgroup" => "ГруппаФормы",
        "checkboxfield" or "checkbox" => "ПолеФормы",
        _ => "ПолеФормы",
    };

    public static string MapFieldView(string? xmlKind) => xmlKind?.ToLowerInvariant() switch
    {
        "checkboxfield" or "checkBox" => "ВидПоляФормы.ПолеФлажка",
        "inputfield" => "ВидПоляФормы.ПолеВвода",
        "labelfield" => "ВидПоляФормы.ПолеНадписи",
        _ => "ВидПоляФормы.ПолеВвода",
    };

    public static string ОписаниеТиповLiteral(string? typeBody)
    {
        if (string.IsNullOrWhiteSpace(typeBody))
            return "Новый ОписаниеТипов(\"Строка\")";

        var t = typeBody.Trim();
        // Простые платформенные типы.
        if (t.Equals("Булево", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Boolean", StringComparison.OrdinalIgnoreCase))
            return "Новый ОписаниеТипов(\"Булево\")";
        if (t.Equals("Строка", StringComparison.OrdinalIgnoreCase)
            || t.Equals("String", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Строка", StringComparison.OrdinalIgnoreCase))
            return "Новый ОписаниеТипов(\"Строка\")";
        if (t.Equals("Число", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Number", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Число", StringComparison.OrdinalIgnoreCase))
            return "Новый ОписаниеТипов(\"Число\")";
        if (t.Equals("Дата", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Date", StringComparison.OrdinalIgnoreCase))
            return "Новый ОписаниеТипов(\"Дата\")";

        // CatalogObject.X / DocumentRef.Y и т.п.
        if (t.Contains('.', StringComparison.Ordinal))
            return "Новый ОписаниеТипов(\"" + t.Replace("\"", "\"\"", StringComparison.Ordinal) + "\")";

        return "Новый ОписаниеТипов(\"" + t.Replace("\"", "\"\"", StringComparison.Ordinal) + "\")";
    }
}
