using System.Text.RegularExpressions;

namespace Sverka;

/// <summary>Простая подсветка BSL для diff-панели (ключевые слова, директивы, комментарии, строки).</summary>
public static class BslHighlighter
{
    public enum TokenKind
    {
        Plain,
        Keyword,
        Directive,
        Comment,
        String,
        Number,
        DiffMarker,
    }

    public sealed record Token(string Text, TokenKind Kind);

    private static readonly Regex Tokenizer = new(
        @"(?<diff>^[\+\-]\s)|(?<dir>#[\wА-Яа-яЁё]+|&[\wА-Яа-яЁё]+)|(?<comment>//[^\r\n]*)|(?<string>""(?:""""|[^""])*"")|(?<number>\b\d+(?:\.\d+)?\b)|(?<word>[\wА-Яа-яЁё]+)|(?<other>[^\wА-Яа-яЁё""#&/\+\-]+|.)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Если", "Тогда", "ИначеЕсли", "Иначе", "КонецЕсли",
        "Пока", "Цикл", "КонецЦикла", "Для", "По", "Каждого", "Из",
        "Процедура", "Функция", "КонецПроцедуры", "КонецФункции",
        "Возврат", "Прервать", "Продолжить", "Перем", "Знач", "Экспорт",
        "Попытка", "Исключение", "КонецПопытки", "ВызватьИсключение",
        "Новый", "Неопределено", "Null", "Истина", "Ложь",
        "И", "Или", "Не", "Тип", "ТипЗнч",
        "Если", "If", "Then", "ElsIf", "Else", "EndIf",
        "While", "Do", "EndDo", "For", "Each", "In", "To",
        "Procedure", "Function", "EndProcedure", "EndFunction",
        "Return", "Break", "Continue", "Var", "Val", "Export",
        "Try", "Except", "EndTry", "Raise",
        "New", "Undefined", "True", "False", "And", "Or", "Not",
        "ПродолжитьВызов", "ProceedWithCall",
        "Перед", "После", "Вместо", "ИзменениеИКонтроль",
        "Before", "After", "Instead", "ChangeAndControl",
        "Вставка", "КонецВставки", "Удаление", "КонецУдаления",
        "Insert", "EndInsert", "Delete", "EndDelete",
    };

    public static IReadOnlyList<Token> Tokenize(string line)
    {
        if (string.IsNullOrEmpty(line))
            return [new Token("", TokenKind.Plain)];

        var tokens = new List<Token>();
        foreach (Match match in Tokenizer.Matches(line))
        {
            if (match.Groups["diff"].Success)
            {
                tokens.Add(new Token(match.Value, TokenKind.DiffMarker));
                continue;
            }

            if (match.Groups["dir"].Success)
            {
                tokens.Add(new Token(match.Value, TokenKind.Directive));
                continue;
            }

            if (match.Groups["comment"].Success)
            {
                tokens.Add(new Token(match.Value, TokenKind.Comment));
                continue;
            }

            if (match.Groups["string"].Success)
            {
                tokens.Add(new Token(match.Value, TokenKind.String));
                continue;
            }

            if (match.Groups["number"].Success)
            {
                tokens.Add(new Token(match.Value, TokenKind.Number));
                continue;
            }

            if (match.Groups["word"].Success)
            {
                var word = match.Value;
                tokens.Add(new Token(word, Keywords.Contains(word) ? TokenKind.Keyword : TokenKind.Plain));
                continue;
            }

            tokens.Add(new Token(match.Value, TokenKind.Plain));
        }

        return tokens.Count == 0 ? [new Token(line, TokenKind.Plain)] : tokens;
    }
}
