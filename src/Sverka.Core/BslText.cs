using System.Text;

namespace Sverka;

public static class BslText
{
    public static IReadOnlyList<string> Lines(string source)
    {
        return source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    public static string Normalize(string text)
    {
        var lines = new List<string>();
        foreach (var raw in Lines(text))
        {
            var stripped = StripComment(raw);
            var lowered = LowerOutsideStrings(stripped).Trim();
            if (lowered.Length > 0)
                lines.Add(lowered);
        }

        return string.Join("\n", lines);
    }

    public static string Collapse(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    public static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString && current == '/' && i + 1 < line.Length && line[i + 1] == '/')
                return line[..i];
        }

        return line;
    }

    public static string LowerOutsideStrings(string line)
    {
        var builder = new StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '"')
            {
                builder.Append(current);
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            builder.Append(inString ? current : char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }

    public static bool ContainsDynamicCall(string source)
    {
        foreach (var line in Lines(source))
        {
            var code = LowerOutsideStrings(StripComment(line));
            if (code.Contains("выполнить(", StringComparison.Ordinal)
                || code.Contains("вычислить(", StringComparison.Ordinal)
                || code.Contains("execute(", StringComparison.Ordinal)
                || code.Contains("eval(", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<(string Module, string Method)> ReadCalls(
        string source,
        IReadOnlyDictionary<string, string> commonModules)
    {
        var calls = new List<(string Module, string Method)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in Lines(source))
        {
            var code = StripComment(raw);
            foreach (var (module, method) in CallsOnLine(code))
            {
                if (!commonModules.TryGetValue(module, out var canonical))
                    continue;
                var key = canonical + "\n" + method;
                if (seen.Add(key))
                    calls.Add((canonical, method));
            }
        }

        return calls;
    }

    private static IEnumerable<(string Module, string Method)> CallsOnLine(string line)
    {
        var inString = false;
        var i = 0;
        while (i < line.Length)
        {
            var current = line[i];
            if (current == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                inString = !inString;
                i++;
                continue;
            }

            if (inString || !IsIdentStart(current))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < line.Length && IsIdentPart(line[i]))
                i++;

            if (i >= line.Length || line[i] != '.')
                continue;

            var module = line[start..i];
            i++;
            if (i >= line.Length || !IsIdentStart(line[i]))
                continue;

            var methodStart = i;
            i++;
            while (i < line.Length && IsIdentPart(line[i]))
                i++;
            var method = line[methodStart..i];

            var cursor = i;
            while (cursor < line.Length && char.IsWhiteSpace(line[cursor]))
                cursor++;
            if (cursor < line.Length && line[cursor] == '(')
                yield return (module, method);
        }
    }

    private static bool IsIdentStart(char value) =>
        value == '_' || char.IsLetter(value);

    private static bool IsIdentPart(char value) =>
        value == '_' || char.IsLetterOrDigit(value);
}
