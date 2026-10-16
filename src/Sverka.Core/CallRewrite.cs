using System.Text;

namespace Sverka;

public sealed record CallHit(
    string ObjectName,
    string ObjectUuid,
    string ObjectType,
    string Module,
    string CallerMethod,
    bool CallerIsFunction,
    string CallerHeader,
    string RewrittenBody,
    int ReplacementCount);

/// <summary>
/// Поиск вызовов метода с числом аргументов больше типового и подмена на префиксное имя.
/// </summary>
public static class CallRewrite
{
    public static IReadOnlyList<CallHit> FindExtraArgCallSites(
        ConfigurationDump customized,
        TransferScope scope,
        string targetObjectFullName,
        string targetMethod,
        int typicalParamCount,
        string prefix)
    {
        var shortModule = ShortName(targetObjectFullName);
        var prefixed = prefix + targetMethod;
        var hits = new List<CallHit>();

        foreach (var owner in customized.Objects)
        {
            if (scope.HasFilter && scope.ObjectNames.Count > 0 && !scope.ObjectNames.Contains(owner.FullName))
                continue;

            foreach (var module in owner.Modules)
            {
                if (IsFormModule(module.RelativePath))
                    continue;

                foreach (var method in module.Methods)
                {
                    var rewritten = RewriteBody(
                        method.BodyText,
                        shortModule,
                        targetMethod,
                        prefixed,
                        typicalParamCount,
                        sameModule: owner.FullName.Equals(targetObjectFullName, StringComparison.OrdinalIgnoreCase),
                        out var count);
                    if (count == 0)
                        continue;

                    hits.Add(new CallHit(
                        owner.FullName,
                        owner.Uuid,
                        owner.Type,
                        module.RelativePath,
                        method.Name,
                        method.IsFunction,
                        HeaderOf(method),
                        rewritten,
                        count));
                }
            }
        }

        return hits;
    }

    public static string RewriteBody(
        string body,
        string targetModuleShortName,
        string targetMethod,
        string prefixedMethod,
        int typicalParamCount,
        bool sameModule,
        out int replacementCount)
    {
        replacementCount = 0;
        var lines = BslText.Lines(body);
        var result = new List<string>();
        foreach (var line in lines)
        {
            result.Add(RewriteLine(
                line,
                targetModuleShortName,
                targetMethod,
                prefixedMethod,
                typicalParamCount,
                sameModule,
                ref replacementCount));
        }

        return string.Join("\n", result);
    }

    private static string RewriteLine(
        string line,
        string targetModuleShortName,
        string targetMethod,
        string prefixedMethod,
        int typicalParamCount,
        bool sameModule,
        ref int replacementCount)
    {
        var code = BslText.StripComment(line);
        var commentIndex = line.Length;
        // Preserve trailing comment if StripComment shortened — reconstruct from original.
        if (code.Length < line.Length && line.AsSpan(code.Length).Contains("//", StringComparison.Ordinal))
        {
            var abs = line.IndexOf("//", StringComparison.Ordinal);
            if (abs >= 0)
                commentIndex = abs;
        }

        var work = commentIndex < line.Length ? line[..commentIndex] : line;
        var comment = commentIndex < line.Length ? line[commentIndex..] : "";

        var builder = new StringBuilder();
        var i = 0;
        var inString = false;
        while (i < work.Length)
        {
            var c = work[i];
            if (c == '"')
            {
                builder.Append(c);
                if (inString && i + 1 < work.Length && work[i + 1] == '"')
                {
                    builder.Append('"');
                    i += 2;
                    continue;
                }

                inString = !inString;
                i++;
                continue;
            }

            if (inString || !IsIdentStart(c))
            {
                builder.Append(c);
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < work.Length && IsIdentPart(work[i]))
                i++;

            // Module.Method(
            if (i < work.Length && work[i] == '.')
            {
                var module = work[start..i];
                var dot = i;
                i++;
                if (i < work.Length && IsIdentStart(work[i]))
                {
                    var methodStart = i;
                    i++;
                    while (i < work.Length && IsIdentPart(work[i]))
                        i++;
                    var method = work[methodStart..i];
                    var cursor = SkipWs(work, i);
                    if (cursor < work.Length && work[cursor] == '('
                        && module.Equals(targetModuleShortName, StringComparison.OrdinalIgnoreCase)
                        && method.Equals(targetMethod, StringComparison.OrdinalIgnoreCase))
                    {
                        var close = FindMatchingParen(work, cursor);
                        if (close > cursor)
                        {
                            var args = work[(cursor + 1)..close];
                            var argCount = BslSignature.ParameterCount($"x({args})");
                            if (argCount > typicalParamCount)
                            {
                                builder.Append(module);
                                builder.Append('.');
                                builder.Append(prefixedMethod);
                                builder.Append(work[cursor..(close + 1)]);
                                i = close + 1;
                                replacementCount++;
                                continue;
                            }
                        }
                    }

                    builder.Append(work[start..i]);
                    continue;
                }

                builder.Append(work[start..dot]);
                i = dot;
                continue;
            }

            // Unqualified Method( — только в том же модуле.
            var ident = work[start..i];
            var after = SkipWs(work, i);
            if (sameModule
                && after < work.Length
                && work[after] == '('
                && ident.Equals(targetMethod, StringComparison.OrdinalIgnoreCase))
            {
                var close = FindMatchingParen(work, after);
                if (close > after)
                {
                    var args = work[(after + 1)..close];
                    var argCount = BslSignature.ParameterCount($"x({args})");
                    if (argCount > typicalParamCount)
                    {
                        builder.Append(prefixedMethod);
                        builder.Append(work[after..(close + 1)]);
                        i = close + 1;
                        replacementCount++;
                        continue;
                    }
                }
            }

            builder.Append(ident);
        }

        return builder + comment;
    }

    private static int FindMatchingParen(string text, int openIndex)
    {
        var depth = 0;
        var inString = false;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
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
            if (c == '(')
                depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int SkipWs(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index;
    }

    private static bool IsIdentStart(char value) => value == '_' || char.IsLetter(value);

    private static bool IsIdentPart(char value) => value == '_' || char.IsLetterOrDigit(value);

    private static string ShortName(string fullName)
    {
        var dot = fullName.IndexOf('.');
        return dot > 0 ? fullName[(dot + 1)..] : fullName;
    }

    private static bool IsFormModule(string path) =>
        path.Contains("Forms/", StringComparison.OrdinalIgnoreCase)
        || path.Contains(@"Forms\", StringComparison.OrdinalIgnoreCase);

    private static string HeaderOf(BslMethod method)
    {
        var kind = method.IsFunction ? "Функция" : "Процедура";
        return $"{kind} {method.Name}()";
    }

    public sealed record UnprovenCall(
        string ObjectName,
        string? ObjectUuid,
        string ObjectType,
        string Module,
        string CallerMethod,
        string Detail);

    /// <summary>
    /// Вызовы, которые нельзя безопасно переписать автоматически:
    /// без префикса модуля вне своего модуля, либо через Выполнить/Вычислить.
    /// </summary>
    public static IReadOnlyList<UnprovenCall> FindUnprovenCallSites(
        ConfigurationDump customized,
        TransferScope scope,
        string targetObjectFullName,
        string targetMethod,
        int typicalParamCount)
    {
        var shortModule = ShortName(targetObjectFullName);
        var result = new List<UnprovenCall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var owner in customized.Objects)
        {
            if (scope.HasFilter && scope.ObjectNames.Count > 0 && !scope.ObjectNames.Contains(owner.FullName))
                continue;

            var sameModule = owner.FullName.Equals(targetObjectFullName, StringComparison.OrdinalIgnoreCase);
            foreach (var module in owner.Modules)
            {
                if (IsFormModule(module.RelativePath))
                    continue;

                foreach (var method in module.Methods)
                {
                    if (BslText.ContainsDynamicCall(method.BodyText)
                        && method.BodyText.Contains(targetMethod, StringComparison.OrdinalIgnoreCase))
                    {
                        var key = owner.FullName + "|" + module.RelativePath + "|" + method.Name + "|dyn";
                        if (seen.Add(key))
                        {
                            result.Add(new UnprovenCall(
                                owner.FullName, owner.Uuid, owner.Type, module.RelativePath, method.Name,
                                $"Не доказано: в теле есть Выполнить/Вычислить и упоминание «{targetMethod}»"));
                        }
                    }

                    if (CountUnqualifiedExtraArgCalls(method.BodyText, targetMethod, typicalParamCount) > 0
                        && !sameModule)
                    {
                        var key = owner.FullName + "|" + module.RelativePath + "|" + method.Name + "|uq";
                        if (seen.Add(key))
                        {
                            result.Add(new UnprovenCall(
                                owner.FullName, owner.Uuid, owner.Type, module.RelativePath, method.Name,
                                $"Не доказано: вызов «{targetMethod}(…)» без модуля (не {shortModule}.) с числом аргументов > типового"));
                        }
                    }
                }
            }
        }

        return result;
    }

    private static int CountUnqualifiedExtraArgCalls(string body, string targetMethod, int typicalParamCount)
    {
        var count = 0;
        foreach (var raw in BslText.Lines(body))
        {
            var work = BslText.StripComment(raw);
            var i = 0;
            var inString = false;
            while (i < work.Length)
            {
                var c = work[i];
                if (c == '"')
                {
                    if (inString && i + 1 < work.Length && work[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    inString = !inString;
                    i++;
                    continue;
                }

                if (inString || !IsIdentStart(c))
                {
                    i++;
                    continue;
                }

                var start = i;
                i++;
                while (i < work.Length && IsIdentPart(work[i]))
                    i++;

                // Module.Method — пропускаем (это квалифицированный вызов).
                if (i < work.Length && work[i] == '.')
                {
                    i++;
                    while (i < work.Length && IsIdentPart(work[i]))
                        i++;
                    continue;
                }

                var ident = work[start..i];
                var after = SkipWs(work, i);
                if (after < work.Length && work[after] == '('
                    && ident.Equals(targetMethod, StringComparison.OrdinalIgnoreCase))
                {
                    var close = FindMatchingParen(work, after);
                    if (close > after)
                    {
                        var args = work[(after + 1)..close];
                        if (BslSignature.ParameterCount($"x({args})") > typicalParamCount)
                            count++;
                        i = close + 1;
                        continue;
                    }
                }
            }
        }

        return count;
    }
}
