namespace Sverka;

/// <summary>
/// Детерминированные эвристики: как выразить доработку тела метода через хук расширения.
/// </summary>
public static class HookSynthesizer
{
    public sealed record Proposal(
        TransferDecision Decision,
        ProposedHook Hook,
        string Reason,
        string? HookBody);

    public static Proposal ForBodyChange(BslMethod typical, BslMethod customized)
    {
        var typicalPairs = MeaningfulPairs(typical.BodyText);
        var customPairs = MeaningfulPairs(customized.BodyText);
        var typicalLines = typicalPairs.Select(item => item.Norm).ToList();
        var customLines = customPairs.Select(item => item.Norm).ToList();

        if (typicalLines.Count == 0 && customLines.Count == 0)
            return new Proposal(TransferDecision.Skip, ProposedHook.None, "Тело пустое с обеих сторон", null);

        if (typicalLines.Count == 0)
            return new Proposal(
                TransferDecision.Manual,
                ProposedHook.None,
                "В типовой версии тело пустое — нужен ручной разбор",
                null);

        if (IsPrefix(typicalLines, customLines))
        {
            var suffix = SliceOriginal(customized.BodyText, typicalLines.Count, keepTail: true);
            return new Proposal(
                TransferDecision.Auto,
                ProposedHook.After,
                "Доработка добавлена после типового тела",
                suffix);
        }

        if (IsSuffix(typicalLines, customLines))
        {
            var prefix = SliceOriginal(customized.BodyText, customLines.Count - typicalLines.Count, keepTail: false);
            return new Proposal(
                TransferDecision.Auto,
                ProposedHook.Before,
                "Доработка добавлена перед типовым телом",
                prefix);
        }

        var insertAnalysis = AnalyzeInserts(typicalLines, customLines);
        if (insertAnalysis.Kind is InsertKind.Single or InsertKind.Multiple
            && insertAnalysis.Gaps.Count > 0)
        {
            var annotated = BuildInsertAnnotatedBody(customized.BodyText, insertAnalysis.Gaps);
            if (!string.IsNullOrWhiteSpace(annotated))
            {
                var reason = insertAnalysis.Gaps.Count == 1
                    ? "Доработка — одна вставка внутри типового тела (&ИзменениеИКонтроль)"
                    : $"Доработка — {insertAnalysis.Gaps.Count} вставки внутри типового тела (&ИзменениеИКонтроль)";
                return new Proposal(
                    TransferDecision.Auto,
                    ProposedHook.ChangeAndControl,
                    reason,
                    annotated);
            }
        }

        // Полная замена с вызовом типовой логики через ПродолжитьВызов → &Вместо.
        if (BodyCallsProceed(customized.BodyText) || customized.CallsProceed)
        {
            return new Proposal(
                TransferDecision.Auto,
                ProposedHook.Instead,
                "Тело заменено и вызывает ПродолжитьВызов — хук &Вместо",
                StripProceedCalls(customized.BodyText));
        }

        // Замены/удаления строк при сохранении общего каркаса → #Вставка/#Удаление.
        var lcs = TryLcsChangeAndControl(typicalPairs, customPairs);
        if (lcs != null)
            return lcs;

        // Нет устойчивого каркаса — полная замена через &Вместо (как Diff&Merge / ручной перенос).
        if (customPairs.Count > 0)
        {
            return new Proposal(
                TransferDecision.Auto,
                ProposedHook.Instead,
                "Тело переписано без общего каркаса — хук &Вместо с телом доработанной",
                customized.BodyText.TrimEnd());
        }

        return new Proposal(
            TransferDecision.Manual,
            ProposedHook.None,
            "Тело изменено так, что безопасный хук не выводится автоматически — смотрите diff",
            null);
    }

    private enum InsertKind
    {
        None,
        Single,
        Multiple,
    }

    private enum LineOpKind
    {
        Equal,
        Delete,
        Insert,
    }

    private sealed record InsertAnalysis(InsertKind Kind, IReadOnlyList<(int From, int To)> Gaps);

    private sealed record LinePair(string Norm, string Original);

    private sealed record LineOp(LineOpKind Kind, string? TypicalOriginal, string? CustomOriginal);

    /// <summary>
    /// LCS типового и доработанного тел: общие строки + #Удаление/#Вставка.
    /// </summary>
    private static Proposal? TryLcsChangeAndControl(
        IReadOnlyList<LinePair> typical,
        IReadOnlyList<LinePair> custom)
    {
        if (typical.Count == 0 || custom.Count == 0)
            return null;

        var ops = DiffLines(typical, custom);
        var equal = 0;
        var deletes = 0;
        var inserts = 0;
        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case LineOpKind.Equal:
                    equal++;
                    break;
                case LineOpKind.Delete:
                    deletes++;
                    break;
                case LineOpKind.Insert:
                    inserts++;
                    break;
            }
        }

        if (equal == 0)
            return null;

        // Слишком мало общего с типовым — лучше &Вместо, не ломаный #Вставка/#Удаление.
        var shared = (double)equal / typical.Count;
        if (shared < 0.12 && equal < 2)
            return null;
        if (deletes == 0 && inserts == 0)
            return null;

        var annotated = RenderLcsBody(ops);
        if (string.IsNullOrWhiteSpace(annotated))
            return null;

        return new Proposal(
            TransferDecision.Auto,
            ProposedHook.ChangeAndControl,
            $"Доработка — правки внутри типового тела (#Вставка/#Удаление), общих строк {equal}/{typical.Count}",
            annotated);
    }

    private static List<LineOp> DiffLines(IReadOnlyList<LinePair> a, IReadOnlyList<LinePair> b)
    {
        var n = a.Count;
        var m = b.Count;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                if (string.Equals(a[i].Norm, b[j].Norm, StringComparison.Ordinal))
                    lcs[i, j] = lcs[i + 1, j + 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var ops = new List<LineOp>();
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (string.Equals(a[x].Norm, b[y].Norm, StringComparison.Ordinal))
            {
                ops.Add(new LineOp(LineOpKind.Equal, a[x].Original, b[y].Original));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                ops.Add(new LineOp(LineOpKind.Delete, a[x].Original, null));
                x++;
            }
            else
            {
                ops.Add(new LineOp(LineOpKind.Insert, null, b[y].Original));
                y++;
            }
        }

        while (x < n)
        {
            ops.Add(new LineOp(LineOpKind.Delete, a[x].Original, null));
            x++;
        }

        while (y < m)
        {
            ops.Add(new LineOp(LineOpKind.Insert, null, b[y].Original));
            y++;
        }

        return ops;
    }

    private static string RenderLcsBody(IReadOnlyList<LineOp> ops)
    {
        var result = new List<string>();
        var i = 0;
        while (i < ops.Count)
        {
            var kind = ops[i].Kind;
            if (kind == LineOpKind.Equal)
            {
                result.Add(ops[i].CustomOriginal ?? ops[i].TypicalOriginal ?? "");
                i++;
                continue;
            }

            var block = new List<string>();
            while (i < ops.Count && ops[i].Kind == kind)
            {
                block.Add(kind == LineOpKind.Delete
                    ? ops[i].TypicalOriginal ?? ""
                    : ops[i].CustomOriginal ?? "");
                i++;
            }

            if (kind == LineOpKind.Delete)
            {
                result.Add("#Удаление");
                result.AddRange(block);
                result.Add("#КонецУдаления");
            }
            else
            {
                result.Add("#Вставка");
                result.AddRange(block);
                result.Add("#КонецВставки");
            }
        }

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[0]))
            result.RemoveAt(0);
        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);
        return string.Join("\n", result);
    }

    private static InsertAnalysis AnalyzeInserts(
        IReadOnlyList<string> typical,
        IReadOnlyList<string> custom)
    {
        if (typical.Count == 0 || custom.Count <= typical.Count)
            return new InsertAnalysis(InsertKind.None, []);

        var matchAt = new int[typical.Count];
        var customIndex = 0;
        for (var t = 0; t < typical.Count; t++)
        {
            while (customIndex < custom.Count && !string.Equals(typical[t], custom[customIndex], StringComparison.Ordinal))
                customIndex++;
            if (customIndex >= custom.Count)
                return new InsertAnalysis(InsertKind.None, []);
            matchAt[t] = customIndex;
            customIndex++;
        }

        var gaps = new List<(int From, int To)>();
        if (matchAt[0] > 0)
            gaps.Add((0, matchAt[0]));
        for (var t = 0; t < typical.Count - 1; t++)
        {
            var from = matchAt[t] + 1;
            var to = matchAt[t + 1];
            if (from < to)
                gaps.Add((from, to));
        }

        var afterLast = matchAt[^1] + 1;
        if (afterLast < custom.Count)
            gaps.Add((afterLast, custom.Count));

        if (gaps.Count == 0)
            return new InsertAnalysis(InsertKind.None, []);
        return new InsertAnalysis(gaps.Count == 1 ? InsertKind.Single : InsertKind.Multiple, gaps);
    }

    private static string BuildInsertAnnotatedBody(
        string customBodyText,
        IReadOnlyList<(int From, int To)> gaps)
    {
        var raw = BslText.Lines(customBodyText);
        var meaningfulIndexes = new List<int>();
        for (var i = 0; i < raw.Count; i++)
        {
            if (BslText.Normalize(raw[i]).Length > 0)
                meaningfulIndexes.Add(i);
        }

        var insertRanges = new List<(int FromRaw, int ToRaw)>();
        foreach (var (meaningfulFrom, meaningfulTo) in gaps)
        {
            if (meaningfulFrom < 0 || meaningfulTo > meaningfulIndexes.Count || meaningfulFrom >= meaningfulTo)
                continue;
            var fromRaw = meaningfulIndexes[meaningfulFrom];
            var toRawExclusive = meaningfulTo < meaningfulIndexes.Count
                ? meaningfulIndexes[meaningfulTo]
                : raw.Count;
            insertRanges.Add((fromRaw, toRawExclusive));
        }

        if (insertRanges.Count == 0)
            return "";

        var result = new List<string>();
        var rangeIndex = 0;
        for (var i = 0; i < raw.Count; i++)
        {
            if (rangeIndex < insertRanges.Count && i == insertRanges[rangeIndex].FromRaw)
                result.Add("#Вставка");
            result.Add(raw[i]);
            if (rangeIndex < insertRanges.Count && i == insertRanges[rangeIndex].ToRaw - 1)
            {
                result.Add("#КонецВставки");
                rangeIndex++;
            }
        }

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[0]))
            result.RemoveAt(0);
        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);
        return string.Join("\n", result);
    }

    private static bool BodyCallsProceed(string body) =>
        BslText.Normalize(body).Contains("продолжитьвызов", StringComparison.Ordinal)
        || BslText.Normalize(body).Contains("proceedwithcall", StringComparison.Ordinal);

    private static string StripProceedCalls(string body)
    {
        var kept = new List<string>();
        foreach (var line in BslText.Lines(body))
        {
            var normalized = BslText.Normalize(line);
            if (normalized.Contains("продолжитьвызов", StringComparison.Ordinal)
                || normalized.Contains("proceedwithcall", StringComparison.Ordinal))
                continue;
            kept.Add(line);
        }

        while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[0]))
            kept.RemoveAt(0);
        while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[^1]))
            kept.RemoveAt(kept.Count - 1);
        return string.Join("\n", kept);
    }

    private static bool IsPrefix(IReadOnlyList<string> typical, IReadOnlyList<string> custom)
    {
        if (typical.Count > custom.Count)
            return false;
        for (var i = 0; i < typical.Count; i++)
        {
            if (!string.Equals(typical[i], custom[i], StringComparison.Ordinal))
                return false;
        }

        return typical.Count < custom.Count;
    }

    private static bool IsSuffix(IReadOnlyList<string> typical, IReadOnlyList<string> custom)
    {
        if (typical.Count > custom.Count)
            return false;
        var offset = custom.Count - typical.Count;
        if (offset == 0)
            return false;
        for (var i = 0; i < typical.Count; i++)
        {
            if (!string.Equals(typical[i], custom[offset + i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static List<LinePair> MeaningfulPairs(string body)
    {
        var result = new List<LinePair>();
        foreach (var raw in BslText.Lines(body))
        {
            var normalized = BslText.Normalize(raw);
            if (normalized.Length > 0)
                result.Add(new LinePair(normalized, raw));
        }

        return result;
    }

    private static string SliceOriginal(string bodyText, int meaningfulCount, bool keepTail)
    {
        var raw = BslText.Lines(bodyText);
        var meaningfulIndexes = new List<int>();
        for (var i = 0; i < raw.Count; i++)
        {
            if (BslText.Normalize(raw[i]).Length > 0)
                meaningfulIndexes.Add(i);
        }

        if (meaningfulCount <= 0 || meaningfulCount >= meaningfulIndexes.Count)
            return bodyText.TrimEnd();

        int fromRaw;
        int toRawExclusive;
        if (keepTail)
        {
            fromRaw = meaningfulIndexes[meaningfulCount];
            toRawExclusive = raw.Count;
        }
        else
        {
            fromRaw = 0;
            toRawExclusive = meaningfulIndexes[meaningfulCount];
        }

        return JoinRaw(raw, fromRaw, toRawExclusive);
    }

    private static string JoinRaw(IReadOnlyList<string> raw, int from, int toExclusive)
    {
        if (from >= toExclusive)
            return "";
        var slice = new List<string>();
        for (var i = from; i < toExclusive; i++)
            slice.Add(raw[i]);
        while (slice.Count > 0 && string.IsNullOrWhiteSpace(slice[0]))
            slice.RemoveAt(0);
        while (slice.Count > 0 && string.IsNullOrWhiteSpace(slice[^1]))
            slice.RemoveAt(slice.Count - 1);
        return string.Join("\n", slice);
    }
}
