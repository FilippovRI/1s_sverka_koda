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
        var typicalLines = MeaningfulLines(typical.BodyText);
        var customLines = MeaningfulLines(customized.BodyText);

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

        if (TryFindSingleInsert(typicalLines, customLines, customized.BodyText, out var insertBody, out var reason))
        {
            return new Proposal(
                TransferDecision.Auto,
                ProposedHook.ChangeAndControl,
                reason,
                insertBody);
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

        return new Proposal(
            TransferDecision.Manual,
            ProposedHook.None,
            "Тело изменено так, что безопасный хук не выводится автоматически",
            null);
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

    /// <summary>
    /// Ищет один непрерывный блок вставки: типовые строки идут подряд в доработанном
    /// теле, а между двумя соседними типовыми (или с краю) есть ровно один фрагмент extra.
    /// </summary>
    private static bool TryFindSingleInsert(
        IReadOnlyList<string> typical,
        IReadOnlyList<string> custom,
        string customBodyText,
        out string insertBody,
        out string reason)
    {
        insertBody = "";
        reason = "";
        if (typical.Count == 0 || custom.Count <= typical.Count)
            return false;

        var matchAt = new int[typical.Count];
        var customIndex = 0;
        for (var t = 0; t < typical.Count; t++)
        {
            while (customIndex < custom.Count && !string.Equals(typical[t], custom[customIndex], StringComparison.Ordinal))
                customIndex++;
            if (customIndex >= custom.Count)
                return false;
            matchAt[t] = customIndex;
            customIndex++;
        }

        // Все типовые строки найдены по порядку. Вставки — промежутки между матчами и по краям.
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

        if (gaps.Count != 1)
            return false;

        var (gapFrom, gapTo) = gaps[0];
        insertBody = SliceOriginalByMeaningfulRange(customBodyText, gapFrom, gapTo);
        if (string.IsNullOrWhiteSpace(insertBody))
            return false;

        reason = "Доработка — одна вставка внутри типового тела";
        return true;
    }

    private static List<string> MeaningfulLines(string body)
    {
        var result = new List<string>();
        foreach (var raw in BslText.Lines(body))
        {
            var normalized = BslText.Normalize(raw);
            if (normalized.Length > 0)
                result.Add(normalized);
        }

        return result;
    }

    /// <summary>
    /// Берёт исходные строки доработанного тела, соответствующие хвосту или голове
    /// по числу значащих строк.
    /// </summary>
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

    private static string SliceOriginalByMeaningfulRange(string bodyText, int meaningfulFrom, int meaningfulTo)
    {
        var raw = BslText.Lines(bodyText);
        var meaningfulIndexes = new List<int>();
        for (var i = 0; i < raw.Count; i++)
        {
            if (BslText.Normalize(raw[i]).Length > 0)
                meaningfulIndexes.Add(i);
        }

        if (meaningfulFrom < 0 || meaningfulTo > meaningfulIndexes.Count || meaningfulFrom >= meaningfulTo)
            return "";

        var fromRaw = meaningfulIndexes[meaningfulFrom];
        var toRawExclusive = meaningfulTo < meaningfulIndexes.Count
            ? meaningfulIndexes[meaningfulTo]
            : raw.Count;
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
