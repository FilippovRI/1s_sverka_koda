using System.Text;

namespace Sverka.Core.Tests;

public class CompareTests
{
    [Fact]
    public void CommentAndBlankLinesDoNotCountAsChange()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                Статус = 1;
            КонецПроцедуры
            """);
        after.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                // комментарий

                Статус = 1;
            КонецПроцедуры
            """);

        var changes = Compare(before, after);
        Assert.DoesNotContain(changes, change => change.Kind == ChangeKind.Module);
    }

    [Fact]
    public void BodyChangeIsReportedOncePerMethod()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = 1;", "Сумма = 1;"));
        after.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = РассчитатьСтатус(Документ, Истина);", "Сумма = 1;"));

        var changes = Compare(before, after);
        var methods = changes.Where(change => change.Kind == ChangeKind.Module).ToList();
        var changed = Assert.Single(methods);
        Assert.Equal("НекотораяПроцедура", changed.Method);
        Assert.Equal(ModuleChangePart.Body, changed.ModulePart);
        Assert.Equal("Ext/Module.bsl", changed.Module);
    }

    [Fact]
    public void SameUuidAndNewNameIsRename()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");
        after.CommonModule("РасчетыНовые", ModuleId, "Процедура Пусто()\nКонецПроцедуры");

        var changes = Compare(before, after);
        Assert.Contains(changes, change => change.Kind == ChangeKind.Renamed && change.Summary.Contains("CommonModule.Расчеты", StringComparison.Ordinal));
        Assert.DoesNotContain(changes, change => change.Kind == ChangeKind.Added);
        Assert.DoesNotContain(changes, change => change.Kind == ChangeKind.Removed && change.ObjectName.Contains("Расчеты", StringComparison.Ordinal));
    }

    [Fact]
    public void DifferentUuidWithSameNameIsRemoveAndAdd()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");
        after.CommonModule("Расчеты", "22222222-2222-2222-2222-222222222222", "Процедура Пусто()\nКонецПроцедуры");

        var changes = Compare(before, after);
        Assert.Contains(changes, change => change.Kind == ChangeKind.Removed);
        Assert.Contains(changes, change => change.Kind == ChangeKind.Added);
    }

    [Fact]
    public void AttributeAdditionAndTypeChangeAreSeparate()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.Catalog("Номенклатура", CatalogId, [("Артикул", "10")]);
        after.Catalog("Номенклатура", CatalogId, [("Артикул", "25"), ("КодТнВэд", "10")]);

        var changes = Compare(before, after);
        Assert.Contains(changes, change => change.MemberDelta == MemberDelta.Added && change.MemberName == "КодТнВэд");
        Assert.Contains(changes, change => change.MemberDelta == MemberDelta.Modified && change.MemberName == "Артикул" && change.Kind == ChangeKind.Properties);
    }

    [Fact]
    public void VersionChangeIsAPropertyOfConfiguration()
    {
        using var before = Dump.Create(version: "1.0.0.1");
        using var after = Dump.Create(version: "1.0.0.2");
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");

        var changes = Compare(before, after);
        Assert.Contains(changes, change => change.ObjectName == "Конфигурация" && change.Summary.Contains("1.0.0.2", StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatedCompareIsIdentical()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = 1;", "Сумма = 1;"));
        after.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = 2;", "Сумма = 3;"));

        var first = Compare(before, after);
        var second = Compare(before, after);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ImpactKeepsOnlyTheHookedMethod()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1", version: "1.0");
        before.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = 1;", "Сумма = 1;"));
        after.CommonModule("Расчеты", ModuleId, TwoMethods("Статус = 2;", "Сумма = 2;"));
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &ИзменениеИКонтроль("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
                #Вставка
                НашаДополнительнаяЛогика();
                #КонецВставки
            КонецПроцедуры
            """);

        var changes = Compare(before, after);
        Assert.Equal(2, changes.Count(change => change.Kind == ChangeKind.Module));
        var findings = Impact(before, after, extension, changes);
        var finding = Assert.Single(findings, item => item.Severity == Severity.Adaptation);
        Assert.Equal("НекотораяПроцедура", finding.Method);
        Assert.Equal("ИзменениеИКонтроль, вставка", finding.Intervention);
        Assert.Equal(ExitCodes.Review, ExitCodes.FromFindings(findings));
    }

    [Fact]
    public void ChangeAndControlStaysAdaptationWhenAnchorLinesTouched()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                А = 1;
                Б = 2;
                В = 3;
            КонецПроцедуры
            """);
        after.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                А = 10;
                Б = 2;
                В = 3;
            КонецПроцедуры
            """);
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &ИзменениеИКонтроль("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
                А = 1;
                #Вставка
                НашаЛогика();
                #КонецВставки
                Б = 2;
                В = 3;
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Adaptation, finding.Severity);
        Assert.Contains("якор", finding.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeAndControlDropsToWarningWhenAnchorLinesSurvive()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                А = 1;
                Б = 2;
                В = 3;
            КонецПроцедуры
            """);
        after.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
                А = 1;
                Б = 2;
                В = 3;
                Г = 4;
            КонецПроцедуры
            """);
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &ИзменениеИКонтроль("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
                А = 1;
                #Вставка
                НашаЛогика();
                #КонецВставки
                Б = 2;
                В = 3;
            КонецПроцедуры
            """);

        var changes = Compare(before, after);
        Assert.Single(changes.Where(change => change.Kind == ChangeKind.Module));
        var finding = Assert.Single(Impact(before, after, extension, changes));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(ExitCodes.Review, ExitCodes.FromFindings([finding]));
    }

    [Fact]
    public void RemovedHookedMethodIsCritical()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Другая()\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Перед("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(ExitCodes.Critical, ExitCodes.FromFindings([finding]));
    }

    /// <summary>
    /// Обработчик &amp;Перед отрабатывает до типового метода и видит только входные
    /// параметры, поэтому новое тело типового метода на него не влияет.
    /// </summary>
    [Fact]
    public void BeforeHookOnBodyChangeIsSilent()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 2;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Перед("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        Assert.Empty(Impact(before, after, extension));
    }

    [Fact]
    public void BeforeHookOnSignatureChangeIsCritical()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ, Отказ)\n\tСтатус = 1;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Перед("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal("Перед", finding.Intervention);
    }

    /// <summary>
    /// Обработчик &amp;После зависит от того, что типовой метод успел сделать,
    /// поэтому новое тело остаётся предупреждением даже у функции.
    /// </summary>
    [Fact]
    public void AfterHookOnBodyChangeIsWarning()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Функция НекотораяФункция(Документ)\n\tВозврат 1;\nКонецФункции");
        after.CommonModule("Расчеты", ModuleId, "Функция НекотораяФункция(Документ)\n\tВозврат 2;\nКонецФункции");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &После("НекотораяФункция")
            Процедура Расш1_НекотораяФункция(Документ, Результат)
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("После", finding.Intervention);
    }

    [Fact]
    public void InsteadWithProceedCallOnBodyChangeIsWarning()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 2;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Вместо("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
                ПродолжитьВызов(Документ);
                НашаЛогика();
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("ПродолжитьВызов", finding.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void InsteadWithoutProceedCallIsSilent()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 2;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Вместо("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
                Статус = 0;
            КонецПроцедуры
            """);

        Assert.Empty(Impact(before, after, extension));
    }

    [Fact]
    public void SignatureChangeOfHookedMethodIsCritical()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ, Отказ)\n\tСтатус = 1;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &После("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(ModuleChangePart.Signature, Compare(before, after).Single(change => change.Kind == ChangeKind.Module).ModulePart);
    }

    [Fact]
    public void CallFromAnotherObjectIsWarning()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 2;\nКонецПроцедуры");
        before.Catalog("Номенклатура", CatalogId, []);
        after.Catalog("Номенклатура", CatalogId, []);
        extension.Catalog("Номенклатура", "33333333-3333-3333-3333-333333333333", [], extended: CatalogId);
        extension.Write("Catalogs/Номенклатура/Ext/ObjectModule.bsl", """
            Процедура ПриЗаписи(Отказ)
                Расчеты.НекотораяПроцедура(Ссылка);
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("вызов", finding.Intervention);
        Assert.Equal("CommonModule.Расчеты", finding.ObjectName);
    }

    [Fact]
    public void DynamicCallIsUncheckedAndDoesNotFailTheRun()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            Процедура Проверка()
                Выполнить("Сообщить(1)");
            КонецПроцедуры
            """);

        var findings = Impact(before, after, extension);
        Assert.Contains(findings, finding => finding.Severity == Severity.Unchecked);
        Assert.Equal(ExitCodes.Ok, ExitCodes.FromFindings(findings));
    }

    [Fact]
    public void RemovedBorrowedAttributeIsCritical()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.Catalog("Номенклатура", CatalogId, [("Артикул", "25")]);
        after.Catalog("Номенклатура", CatalogId, []);
        extension.Catalog("Номенклатура", "33333333-3333-3333-3333-333333333333", [("Артикул", "25")], extended: CatalogId);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal("заимствование", finding.Intervention);
    }

    /// <summary>
    /// Синоним и комментарий не меняют ни тип, ни длину реквизита, поэтому
    /// заимствовавшему его расширению такая правка ничем не грозит.
    /// </summary>
    [Fact]
    public void CosmeticChangeOfBorrowedAttributeIsSilent()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.Catalog("Номенклатура", CatalogId, [new Attr("Артикул", "25", Synonym: "Артикул", Comment: "")]);
        after.Catalog("Номенклатура", CatalogId, [new Attr("Артикул", "25", Synonym: "Артикул поставщика", Comment: "Уточнили название")]);
        extension.Catalog("Номенклатура", ExtensionCatalogId, [("Артикул", "25")], extended: CatalogId);

        Assert.Contains(
            Compare(before, after),
            change => change.MemberName == "Артикул" && change.MemberDelta == MemberDelta.Modified);
        Assert.Empty(Impact(before, after, extension));
    }

    [Fact]
    public void TypeChangeOfBorrowedAttributeIsWarning()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.Catalog("Номенклатура", CatalogId, [new Attr("Артикул", "25", Synonym: "Артикул")]);
        after.Catalog("Номенклатура", CatalogId, [new Attr("Артикул", "10", Synonym: "Артикул поставщика")]);
        extension.Catalog("Номенклатура", ExtensionCatalogId, [("Артикул", "25")], extended: CatalogId);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("заимствование", finding.Intervention);
    }

    [Fact]
    public void JsonUsesRussianKinds()
    {
        using var before = Dump.Create(version: "1");
        using var after = Dump.Create(version: "2");
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\n\tА = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\n\tА = 2;\nКонецПроцедуры");
        var directory = Path.Combine(Path.GetTempPath(), "sverka-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var changes = Compare(before, after);
            var jsonPath = Path.Combine(directory, "config.json");
            ReportWriter.WriteConfig(jsonPath, before.Read(), after.Read(), changes);
            var json = File.ReadAllText(jsonPath);
            Assert.Contains("\"Модуль\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Свойства\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SummarizeGroupsFindingsPerExtensionAndKeepsCleanOnes()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Другая()\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &Перед("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        var findings = Impact(before, after, extension);
        var summaries = ExtensionAnalyzer.Summarize(["Расш1", "ЧистоеРасширение"], findings);

        Assert.Equal(2, summaries.Count);
        var dirty = Assert.Single(summaries, item => item.Extension == "Расш1");
        Assert.Equal(1, dirty.Critical);
        Assert.Equal(1, dirty.Total);

        var clean = Assert.Single(summaries, item => item.Extension == "ЧистоеРасширение");
        Assert.Equal(0, clean.Total);

        // Проблемное расширение идёт раньше чистого.
        Assert.Equal("Расш1", summaries[0].Extension);
        Assert.Equal("ЧистоеРасширение", summaries[1].Extension);
    }

    private static IReadOnlyList<ConfigChange> Compare(Dump before, Dump after) =>
        ConfigComparer.Compare(before.Read(), after.Read());

    private static IReadOnlyList<ImpactFinding> Impact(Dump before, Dump after, Dump extension, IReadOnlyList<ConfigChange>? changes = null)
    {
        var beforeDump = before.Read();
        var afterDump = after.Read();
        changes ??= ConfigComparer.Compare(beforeDump, afterDump);
        return ExtensionAnalyzer.Analyze(beforeDump, afterDump, changes, [extension.Read()]);
    }

    private static string TwoMethods(string firstBody, string secondBody) =>
        $"""
        Процедура НекотораяПроцедура(Документ)
            {firstBody}
        КонецПроцедуры

        Процедура ДругаяПроцедура(Документ)
            {secondBody}
        КонецПроцедуры
        """;

    private const string ModuleId = "11111111-1111-1111-1111-111111111111";
    private const string CatalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string ExtensionCatalogId = "33333333-3333-3333-3333-333333333333";
}

/// <summary>
/// Реквизит справочника для помощника Dump. Кортеж («Имя», «Длина») приводится
/// к Attr неявно, поэтому старые вызовы вида [("Артикул", "25")] работают как раньше.
/// </summary>
sealed record Attr(string Name, string Length = "10", string Synonym = "", string Comment = "")
{
    public static implicit operator Attr((string Name, string Length) value) => new(value.Name, value.Length);
}

sealed class Dump : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sverka-" + Guid.NewGuid().ToString("N"));

    private Dump(string name, string version)
    {
        Directory.CreateDirectory(_root);
        Write("Configuration.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <MetaDataObject>
              <Configuration uuid="cccccccc-cccc-cccc-cccc-cccccccccccc">
                <Properties>
                  <Name>{name}</Name>
                  <Version>{version}</Version>
                  <Vendor>Тест</Vendor>
                  <CompatibilityMode>Version8_3_24</CompatibilityMode>
                </Properties>
              </Configuration>
            </MetaDataObject>
            """);
    }

    public static Dump Create(string name = "Демо", string version = "1.0.0.1") => new(name, version);

    public string Root => _root;

    public ConfigurationDump Read() => DumpReader.Read(_root);

    public void Write(string relative, string content)
    {
        var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void CommonModule(string name, string uuid, string module)
    {
        Write($"CommonModules/{name}.xml", ObjectXml("CommonModule", name, uuid, ""));
        Write($"CommonModules/{name}/Ext/Module.bsl", module);
    }

    public void ExtensionModule(string extensionName, string name, string baseUuid, string module)
    {
        Write("Configuration.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <MetaDataObject>
              <Configuration uuid="dddddddd-dddd-dddd-dddd-dddddddddddd">
                <Properties>
                  <Name>{extensionName}</Name>
                  <Version>1.0</Version>
                  <Vendor></Vendor>
                  <CompatibilityMode>Version8_3_24</CompatibilityMode>
                </Properties>
              </Configuration>
            </MetaDataObject>
            """);
        Write($"CommonModules/{name}.xml", ObjectXml("CommonModule", name, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", baseUuid));
        Write($"CommonModules/{name}/Ext/Module.bsl", module);
    }

    public void Catalog(
        string name,
        string uuid,
        Attr[] attributes,
        string? extended = null,
        string[]? forms = null)
    {
        var children = string.Join("\n", attributes.Select((attribute, index) => $"""
              <Attribute uuid="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb{index}">
                <Properties>
                  <Name>{attribute.Name}</Name>{Optional("Synonym", attribute.Synonym)}{Optional("Comment", attribute.Comment)}
                  <Type>
                    <Length>{attribute.Length}</Length>
                  </Type>
                </Properties>
              </Attribute>
            """));
        if (forms != null)
        {
            var formXml = string.Join("\n", forms.Select(form => $"  <Form>{form}</Form>"));
            children = children.Length == 0 ? formXml : children + "\n" + formXml;
        }

        Write($"Catalogs/{name}.xml", ObjectXml("Catalog", name, uuid, children, extended));
    }

    /// <summary>Пишет Forms/&lt;Имя&gt;/Ext/Form.xml объекта. body — содержимое элемента Form.</summary>
    public void Form(string objectPath, string formName, string body)
    {
        Write($"{objectPath}/Forms/{formName}/Ext/Form.xml", $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Form>
            {body}
            </Form>
            """);
    }

    /// <summary>Сохраняет прогон так же, как это делает кнопка «Сохранить отчёт».</summary>
    public static string SaveRun(string directory, Dump before, Dump after, Dump? extension = null)
    {
        var beforeDump = before.Read();
        var afterDump = after.Read();
        var changes = ConfigComparer.Compare(beforeDump, afterDump);
        Directory.CreateDirectory(directory);
        ReportWriter.WriteConfig(Path.Combine(directory, "config.json"), beforeDump, afterDump, changes);
        if (extension != null)
        {
            var findings = ExtensionAnalyzer.Analyze(beforeDump, afterDump, changes, [extension.Read()]);
            ReportWriter.WriteImpact(Path.Combine(directory, "impact.json"), findings);
        }

        return directory;
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static string Optional(string tag, string value) =>
        value.Length == 0 ? "" : $"\n      <{tag}>{value}</{tag}>";

    private static string ObjectXml(string type, string name, string uuid, string children, string? extended = null)
    {
        var extendedXml = extended == null ? "" : $"<ExtendedConfigurationObject>{extended}</ExtendedConfigurationObject>";
        var belonging = extended == null ? "" : "<ObjectBelonging>Adopted</ObjectBelonging>";
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <MetaDataObject>
              <{type} uuid="{uuid}">
                <Properties>
                  <Name>{name}</Name>
                  {belonging}
                  {extendedXml}
                </Properties>
                <ChildObjects>
            {children}
                </ChildObjects>
              </{type}>
            </MetaDataObject>
            """;
    }
}
