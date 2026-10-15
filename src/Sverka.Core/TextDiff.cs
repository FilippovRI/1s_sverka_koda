namespace Sverka;

/// <summary>
/// Построчный diff для панелей «Было/Стало»: выравнивание и маркеры − / + / пробел.
/// </summary>
public static class TextDiff
{
    public static (string Left, string Right) MarkSides(string? before, string? after)
    {
        var leftLines = SplitLines(before);
        var rightLines = SplitLines(after);
        if (leftLines.Count == 0 && rightLines.Count == 0)
            return ("", "");

        var ops = Diff(leftLines, rightLines);
        var left = new List<string>();
        var right = new List<string>();
        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case DiffOpKind.Equal:
                    left.Add("  " + op.Text);
                    right.Add("  " + op.Text);
                    break;
                case DiffOpKind.Delete:
                    left.Add("- " + op.Text);
                    right.Add("  ");
                    break;
                case DiffOpKind.Insert:
                    left.Add("  ");
                    right.Add("+ " + op.Text);
                    break;
            }
        }

        return (string.Join("\n", left), string.Join("\n", right));
    }

    private enum DiffOpKind
    {
        Equal,
        Delete,
        Insert,
    }

    private sealed record DiffOp(DiffOpKind Kind, string Text);

    private static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        return BslText.Lines(text).ToList();
    }

    /// <summary>LCS-based diff — тела методов короткие, O(n·m) достаточно.</summary>
    private static List<DiffOp> Diff(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var n = a.Count;
        var m = b.Count;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                if (string.Equals(a[i], b[j], StringComparison.Ordinal))
                    lcs[i, j] = lcs[i + 1, j + 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var ops = new List<DiffOp>();
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (string.Equals(a[x], b[y], StringComparison.Ordinal))
            {
                ops.Add(new DiffOp(DiffOpKind.Equal, a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                ops.Add(new DiffOp(DiffOpKind.Delete, a[x]));
                x++;
            }
            else
            {
                ops.Add(new DiffOp(DiffOpKind.Insert, b[y]));
                y++;
            }
        }

        while (x < n)
        {
            ops.Add(new DiffOp(DiffOpKind.Delete, a[x]));
            x++;
        }

        while (y < m)
        {
            ops.Add(new DiffOp(DiffOpKind.Insert, b[y]));
            y++;
        }

        return ops;
    }
}
