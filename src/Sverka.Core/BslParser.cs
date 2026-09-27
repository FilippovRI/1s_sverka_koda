using System.Text.RegularExpressions;

namespace Sverka;

public static class BslParser
{
    private static readonly Regex HeaderStart = new(
        @"^\s*(?<kind>Процедура|Функция|Procedure|Function)\s+(?<name>[\p{L}_][\p{L}\p{N}_]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex HookPattern = new(
        @"^&(?<kind>Перед|После|Вместо|ИзменениеИКонтроль|Before|After|Instead|ChangeAndControl)\s*\(\s*""(?<name>[^""]+)""\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static CodeModule Parse(string relativePath, string source)
    {
        var lines = BslText.Lines(source);
        var methods = new List<BslMethod>();
        var pending = new List<string>();
        var index = 0;
        while (index < lines.Count)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            if (trimmed.StartsWith('&'))
            {
                pending.Add(BslText.StripComment(trimmed).Trim());
                index++;
                continue;
            }

            var start = HeaderStart.Match(trimmed);
            if (!start.Success)
            {
                pending.Clear();
                index++;
                continue;
            }

            var isFunction = IsFunction(start.Groups["kind"].Value);
            var name = start.Groups["name"].Value;
            var header = new List<string> { lines[index] };
            var combined = lines[index];
            while (!HeaderClosed(combined) && index + 1 < lines.Count)
            {
                index++;
                header.Add(lines[index]);
                combined += "\n" + lines[index];
            }

            index++;
            var body = new List<string>();
            while (index < lines.Count && !IsMethodEnd(lines[index], isFunction))
            {
                body.Add(lines[index]);
                index++;
            }

            if (index < lines.Count)
                index++;

            methods.Add(Build(name, isFunction, header, body, pending, string.Join("\n", body)));
            pending.Clear();
        }

        return new CodeModule(relativePath.Replace('\\', '/'), source, methods);
    }

    private static BslMethod Build(
        string name,
        bool isFunction,
        List<string> header,
        List<string> body,
        List<string> directives,
        string bodyText)
    {
        var hooks = new List<MethodHook>();
        foreach (var directive in directives)
        {
            var match = HookPattern.Match(directive);
            if (!match.Success)
                continue;
            hooks.Add(new MethodHook(ParseKind(match.Groups["kind"].Value), match.Groups["name"].Value));
        }

        return new BslMethod(
            name,
            isFunction,
            BslText.Collapse(BslText.Normalize(string.Join("\n", header))),
            BslText.Normalize(bodyText),
            hooks,
            HasRegion(body, "Вставка", "Insert"),
            HasRegion(body, "Удаление", "Delete"),
            BodyCallsProceed(bodyText),
            bodyText);
    }

    private static bool BodyCallsProceed(string body)
    {
        var normalized = BslText.LowerOutsideStrings(body);
        return normalized.Contains("продолжитьвызов", StringComparison.Ordinal)
            || normalized.Contains("proceedwithcall", StringComparison.Ordinal);
    }

    private static bool HasRegion(List<string> body, string russian, string english)
    {
        foreach (var line in body)
        {
            var code = BslText.StripComment(line).Trim();
            if (code.StartsWith("#" + russian, StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("#" + english, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HookKind ParseKind(string value)
    {
        if (value.Equals("Перед", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Before", StringComparison.OrdinalIgnoreCase))
            return HookKind.Before;
        if (value.Equals("После", StringComparison.OrdinalIgnoreCase)
            || value.Equals("After", StringComparison.OrdinalIgnoreCase))
            return HookKind.After;
        if (value.Equals("Вместо", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Instead", StringComparison.OrdinalIgnoreCase))
            return HookKind.Instead;
        return HookKind.ChangeAndControl;
    }

    private static bool IsFunction(string kind) =>
        kind.Equals("Функция", StringComparison.OrdinalIgnoreCase)
        || kind.Equals("Function", StringComparison.OrdinalIgnoreCase);

    private static bool IsMethodEnd(string line, bool isFunction)
    {
        var code = BslText.StripComment(line).Trim();
        if (isFunction)
        {
            return code.Equals("КонецФункции", StringComparison.OrdinalIgnoreCase)
                || code.Equals("EndFunction", StringComparison.OrdinalIgnoreCase);
        }

        return code.Equals("КонецПроцедуры", StringComparison.OrdinalIgnoreCase)
            || code.Equals("EndProcedure", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HeaderClosed(string text)
    {
        var depth = 0;
        var started = false;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (current == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (inString)
                continue;
            if (current == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                    i++;
                continue;
            }

            if (current == '(')
            {
                depth++;
                started = true;
            }
            else if (current == ')')
            {
                depth--;
            }
        }

        return started && depth <= 0;
    }
}
