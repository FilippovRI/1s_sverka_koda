using System.Text;

namespace Sverka;

/// <summary>
/// Черновик программного создания доработок формы (реквизиты, элементы, команды).
/// XML формы в расширение не пишется — только BSL-заготовка для модуля формы.
/// </summary>
public static class FormProgrammaticWriter
{
    public static void Write(
        string directory,
        TransferPlan plan,
        IReadOnlySet<string>? onlyItemKeys = null)
    {
        Directory.CreateDirectory(directory);
        var items = plan.Items
            .Where(item => item.Decision == TransferDecision.FormProgrammatic)
            .Where(item => onlyItemKeys == null || onlyItemKeys.Contains(TransferSession.ItemKey(item)))
            .ToList();

        if (items.Count == 0)
        {
            File.WriteAllText(
                Path.Combine(directory, "README.txt"),
                "Нет пунктов «Форма» в реестре. Добавленные реквизиты/кнопки форм появятся после поиска доработок.\n",
                Utf8NoBom());
            return;
        }

        var byForm = items.GroupBy(
            item => (Object: item.ObjectName, Form: FormNameOf(item.Method)),
            comparer: new FormKeyComparer());

        foreach (var group in byForm)
        {
            var fileName = Sanitize(ShortName(group.Key.Object) + "_" + group.Key.Form) + ".bsl";
            var path = Path.Combine(directory, fileName);
            var text = RenderFormModule(plan, group.Key.Object, group.Key.Form, group.ToList());
            if (File.Exists(path))
                text = MergeProcedures(File.ReadAllText(path), text);
            File.WriteAllText(path, text, Utf8NoBom());
        }
    }

    /// <summary>Фрагмент для панели «В расширение» по одной строке реестра.</summary>
    public static string PreviewFragment(string prefix, TransferItem item) =>
        RenderSinglePart(prefix, item);

    public static string FormNameOf(string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            return "Форма";
        var dot = memberName.IndexOf('.');
        return dot > 0 ? memberName[..dot] : memberName;
    }

    public static string PartNameOf(string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
            return "Часть";
        var dot = memberName.IndexOf('.');
        return dot > 0 ? memberName[(dot + 1)..] : memberName;
    }

    private static string RenderFormModule(
        TransferPlan plan,
        string objectName,
        string formName,
        IReadOnlyList<TransferItem> parts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Черновик программного создания доработок формы. Сформирован Сверкой.");
        sb.AppendLine("// Объект: " + objectName + ", форма: " + formName);
        sb.AppendLine("// Расширение: " + plan.ExtensionName + ", префикс: " + plan.Prefix);
        sb.AppendLine("// Вставьте в модуль формы расширения (или общий модуль) и доработайте родителя/типы.");
        sb.AppendLine("// Не загружайте XML формы «как есть» — цель именно программное создание.");
        sb.AppendLine();

        var attrs = parts.Where(item => IsKind(item, "FormAttribute")).ToList();
        var items = parts.Where(item => IsKind(item, "FormItem")).ToList();
        var commands = parts.Where(item => IsKind(item, "FormCommand")).ToList();

        var procName = plan.Prefix + "ПриСозданииНаСервере_" + Sanitize(formName);
        sb.AppendLine("&НаСервере");
        sb.AppendLine("Процедура " + procName + "(Отказ, СтандартнаяОбработка)");
        sb.AppendLine();

        if (attrs.Count > 0)
        {
            sb.AppendLine("\t// --- Реквизиты формы ---");
            sb.AppendLine("\tДобавляемыеРеквизиты = Новый Массив;");
            foreach (var item in attrs)
            {
                var name = LastSegment(PartNameOf(item.Method));
                var prefixed = plan.Prefix + name;
                sb.AppendLine("\t// " + item.Reason);
                sb.AppendLine("\tДобавляемыеРеквизиты.Добавить(Новый РеквизитФормы(\"" + Escape(prefixed) + "\", Новый ОписаниеТипов(\"Булево\"))); // TODO: тип");
            }

            sb.AppendLine("\tИзменитьРеквизиты(ДобавляемыеРеквизиты);");
            sb.AppendLine();
        }

        if (items.Count > 0)
        {
            sb.AppendLine("\t// --- Элементы формы ---");
            foreach (var item in items)
            {
                var path = PartNameOf(item.Method);
                var name = LastSegment(path);
                var prefixed = plan.Prefix + name;
                sb.AppendLine("\t// " + item.Reason + " (" + path + ")");
                sb.AppendLine("\tЭлемент = Элементы.Добавить(\"" + Escape(prefixed) + "\", Тип(\"ПолеФормы\")); // TODO: родитель, Тип(\"КнопкаФормы\")/ГруппаФормы");
                sb.AppendLine("\tЭлемент.Вид = ВидПоляФормы.ПолеВвода; // TODO");
                sb.AppendLine("\t// Элемент.ПутьКДанным = \"" + Escape(prefixed) + "\";");
                sb.AppendLine();
            }
        }

        if (commands.Count > 0)
        {
            sb.AppendLine("\t// --- Команды формы ---");
            foreach (var item in commands)
            {
                var name = LastSegment(PartNameOf(item.Method));
                var prefixed = plan.Prefix + name;
                sb.AppendLine("\t// " + item.Reason);
                sb.AppendLine("\tКоманда = Команды.Добавить(\"" + Escape(prefixed) + "\");");
                sb.AppendLine("\tКоманда.Действие = \"" + Escape(prefixed) + "\";");
                sb.AppendLine("\tКнопка = Элементы.Добавить(\"" + Escape(prefixed) + "\", Тип(\"КнопкаФормы\")); // TODO: родитель");
                sb.AppendLine("\tКнопка.ИмяКоманды = \"" + Escape(prefixed) + "\";");
                sb.AppendLine();
            }
        }

        if (attrs.Count == 0 && items.Count == 0 && commands.Count == 0)
            sb.AppendLine("\t// Нет распознанных частей формы.");

        sb.AppendLine("КонецПроцедуры");
        sb.AppendLine();

        foreach (var item in commands)
        {
            var name = plan.Prefix + LastSegment(PartNameOf(item.Method));
            sb.AppendLine("&НаКлиенте");
            sb.AppendLine("Процедура " + name + "(Команда)");
            sb.AppendLine("\t// TODO: обработчик команды формы");
            sb.AppendLine("КонецПроцедуры");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderSinglePart(string prefix, TransferItem item)
    {
        var kind = item.MemberKind ?? "";
        var part = PartNameOf(item.Method);
        var name = prefix + LastSegment(part);
        if (kind.Equals("FormAttribute", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join("\n",
                "// Реквизит формы: " + part,
                "ДобавляемыеРеквизиты = Новый Массив;",
                "ДобавляемыеРеквизиты.Добавить(Новый РеквизитФормы(\"" + name + "\", Новый ОписаниеТипов(\"Булево\"))); // TODO: тип",
                "ИзменитьРеквизиты(ДобавляемыеРеквизиты);");
        }

        if (kind.Equals("FormCommand", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join("\n",
                "// Команда формы: " + part,
                "Команда = Команды.Добавить(\"" + name + "\");",
                "Команда.Действие = \"" + name + "\";",
                "Кнопка = Элементы.Добавить(\"" + name + "\", Тип(\"КнопкаФормы\")); // TODO: родитель",
                "Кнопка.ИмяКоманды = \"" + name + "\";");
        }

        return string.Join("\n",
            "// Элемент формы: " + part,
            "Элемент = Элементы.Добавить(\"" + name + "\", Тип(\"ПолеФормы\")); // TODO: родитель",
            "Элемент.Вид = ВидПоляФормы.ПолеВвода; // TODO",
            "// Элемент.ПутьКДанным = \"" + name + "\";");
    }

    private static bool IsKind(TransferItem item, string kind) =>
        string.Equals(item.MemberKind, kind, StringComparison.OrdinalIgnoreCase);

    private static string LastSegment(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot >= 0 ? path[(dot + 1)..] : path;
    }

    private static string ShortName(string fullName)
    {
        var dot = fullName.IndexOf('.');
        return dot > 0 ? fullName[(dot + 1)..] : fullName;
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        return new string(chars);
    }

    private static string Escape(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string MergeProcedures(string existing, string incoming)
    {
        // Простое дополнение: если процедура уже есть — оставляем файл как есть + комментарий.
        if (existing.Contains("Процедура ", StringComparison.OrdinalIgnoreCase)
            && incoming.Contains("Процедура ", StringComparison.OrdinalIgnoreCase))
        {
            return existing.TrimEnd() + "\n\n// --- дополнение Сверки ---\n" + incoming;
        }

        return incoming;
    }

    private static UTF8Encoding Utf8NoBom() => new(encoderShouldEmitUTF8Identifier: false);

    private sealed class FormKeyComparer : IEqualityComparer<(string Object, string Form)>
    {
        public bool Equals((string Object, string Form) x, (string Object, string Form) y) =>
            x.Object.Equals(y.Object, StringComparison.OrdinalIgnoreCase)
            && x.Form.Equals(y.Form, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Object, string Form) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Object),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Form));
    }
}
