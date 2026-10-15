using System.Text;

namespace Sverka;

/// <summary>
/// Пишет BSL-заготовку порционного переноса данных по пунктам «Данные».
/// В базу не подключается — только файловый шаблон для доработки вручную.
/// </summary>
public static class DataMigrationWriter
{
    public static void Write(string directory, TransferPlan plan)
    {
        Directory.CreateDirectory(directory);
        var items = plan.Items
            .Where(item => item.Decision == TransferDecision.DataMigration)
            .OrderBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();

        var path = Path.Combine(directory, "ПереносДанных.bsl");
        File.WriteAllText(path, Render(plan, items), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Render(TransferPlan plan, IReadOnlyList<TransferItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Черновик обработки переноса данных. Сформирован Сверкой.");
        sb.AppendLine("// Расширение: " + plan.ExtensionName + ", префикс: " + plan.Prefix);
        sb.AppendLine("// Не запускайте «как есть» на рабочей базе: доработайте соответствие старых и новых реквизитов.");
        sb.AppendLine("// Порядок: расширение параллельно → перенос → сверка выборок → очистка основной → замок.");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("// В реестре нет пунктов «Данные». Добавленные реквизиты появятся здесь после поиска доработок.");
            sb.AppendLine();
            sb.AppendLine("Процедура ВыполнитьПеренос() Экспорт");
            sb.AppendLine("\t// Нечего переносить.");
            sb.AppendLine("КонецПроцедуры");
            return sb.ToString();
        }

        sb.AppendLine("// Реестр к переносу:");
        foreach (var item in items)
        {
            var member = item.Method ?? "?";
            sb.AppendLine($"// - {item.ObjectName}.{member} ({item.MemberKind ?? "реквизит"}): {item.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("Процедура ВыполнитьПеренос() Экспорт");
        sb.AppendLine();

        var byObject = items.GroupBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byObject)
        {
            var shortName = ShortName(group.Key);
            var manager = GuessManager(group.Key);
            sb.AppendLine("\t// --- " + group.Key + " ---");
            sb.AppendLine("\t// Выборка = " + manager + "." + shortName + ".Выбрать();");
            sb.AppendLine("\t// Пока Выборка.Следующий() Цикл");
            sb.AppendLine("\t// \tОбъект = Выборка.ПолучитьОбъект();");
            foreach (var item in group)
            {
                var member = item.Method ?? "Реквизит";
                sb.AppendLine("\t// \t// TODO: Объект." + member + " = <значение из старого места>;");
            }

            sb.AppendLine("\t// \tОбъект.Записать();");
            sb.AppendLine("\t// \t// При больших объёмах — коммиты пакетами и обработку прерывания пользователя.");
            sb.AppendLine("\t// КонецЦикла;");
            sb.AppendLine();
        }

        sb.AppendLine("КонецПроцедуры");
        return sb.ToString();
    }

    private static string ShortName(string fullName)
    {
        var dot = fullName.IndexOf('.');
        return dot > 0 ? fullName[(dot + 1)..] : fullName;
    }

    private static string GuessManager(string fullName)
    {
        var type = fullName.Contains('.') ? fullName[..fullName.IndexOf('.')] : "";
        return type.ToLowerInvariant() switch
        {
            "catalog" => "Справочники",
            "document" => "Документы",
            "informationregister" => "РегистрыСведений",
            "accumulationregister" => "РегистрыНакопления",
            "constant" => "Константы",
            _ => "Метаданные",
        };
    }
}
