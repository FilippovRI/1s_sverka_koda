namespace Sverka;

/// <summary>Разбор сигнатур BSL: число параметров.</summary>
public static class BslSignature
{
    public static int ParameterCount(string? signatureNormalized)
    {
        if (string.IsNullOrWhiteSpace(signatureNormalized))
            return 0;

        var open = signatureNormalized.IndexOf('(');
        var close = signatureNormalized.LastIndexOf(')');
        if (open < 0 || close <= open)
            return 0;

        var inside = signatureNormalized[(open + 1)..close].Trim();
        if (inside.Length == 0)
            return 0;

        var count = 1;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < inside.Length; i++)
        {
            var c = inside[i];
            if (c == '"')
            {
                if (inString && i + 1 < inside.Length && inside[i + 1] == '"')
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
                depth--;
            else if (c == ',' && depth == 0)
                count++;
        }

        return count;
    }

    public static bool HasExtraParameters(BslMethod typical, BslMethod customized) =>
        ParameterCount(customized.SignatureNormalized) > ParameterCount(typical.SignatureNormalized);
}
