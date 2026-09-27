namespace Sverka.Core.Tests;

public class FormPartsTests
{
    [Fact]
    public void FormItemsAreComparedOneByOne()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        after.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        before.Form("Catalogs/Номенклатура", "ФормаЭлемента", FormBody("""
                <InputField name="Артикул" id="5">
                  <Title>Артикул</Title>
                </InputField>
                <InputField name="Цена" id="6" />
            """));
        after.Form("Catalogs/Номенклатура", "ФормаЭлемента", FormBody("""
                <InputField name="Артикул" id="5">
                  <Title>Артикул поставщика</Title>
                </InputField>
                <InputField name="Код" id="7" />
            """));

        var changes = ConfigComparer.Compare(before.Read(), after.Read());

        Assert.Contains(changes, change =>
            change.MemberKind == "FormItem"
            && change.MemberName == "ФормаЭлемента.Код"
            && change.MemberDelta == MemberDelta.Added
            && change.Kind == ChangeKind.Composition);
        Assert.Contains(changes, change =>
            change.MemberKind == "FormItem"
            && change.MemberName == "ФормаЭлемента.Цена"
            && change.MemberDelta == MemberDelta.Removed);
        Assert.Contains(changes, change =>
            change.MemberKind == "FormItem"
            && change.MemberName == "ФормаЭлемента.Артикул"
            && change.MemberDelta == MemberDelta.Modified
            && change.Summary.Contains("элемент формы", StringComparison.Ordinal));
        Assert.DoesNotContain(changes, change => change.MemberKind == "Form");
    }

    [Fact]
    public void FormCommandsAndAttributesAreSeparateMembers()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        after.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        before.Form("Catalogs/Номенклатура", "ФормаЭлемента", """
              <Attributes>
                <Attribute name="Объект" id="1">
                  <Type>CatalogObject.Номенклатура</Type>
                </Attribute>
              </Attributes>
              <Commands>
                <Command name="Заполнить" id="1">
                  <Action>Заполнить</Action>
                </Command>
              </Commands>
            """);
        after.Form("Catalogs/Номенклатура", "ФормаЭлемента", """
              <Attributes>
                <Attribute name="Объект" id="1">
                  <Type>CatalogObject.Номенклатура</Type>
                </Attribute>
                <Attribute name="Служебный" id="2">
                  <Type>Булево</Type>
                </Attribute>
              </Attributes>
              <Commands>
                <Command name="Заполнить" id="1">
                  <Action>ЗаполнитьПоШаблону</Action>
                </Command>
              </Commands>
            """);

        var changes = ConfigComparer.Compare(before.Read(), after.Read());

        Assert.Contains(changes, change =>
            change.MemberKind == "FormAttribute"
            && change.MemberName == "ФормаЭлемента.Служебный"
            && change.MemberDelta == MemberDelta.Added
            && change.Summary.Contains("реквизит формы", StringComparison.Ordinal));
        var command = Assert.Single(changes, change => change.MemberKind == "FormCommand");
        Assert.Equal("ФормаЭлемента.Заполнить", command.MemberName);
        Assert.Equal(MemberDelta.Modified, command.MemberDelta);
        Assert.Equal("Изменилась команда формы ФормаЭлемента.Заполнить", command.Summary);
    }

    [Fact]
    public void FormOwnPropertiesStillGiveOneLine()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        after.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        before.Form("Catalogs/Номенклатура", "ФормаЭлемента", "  <Title>Номенклатура</Title>");
        after.Form("Catalogs/Номенклатура", "ФормаЭлемента", "  <Title>Карточка номенклатуры</Title>");

        var changes = ConfigComparer.Compare(before.Read(), after.Read());

        var form = Assert.Single(changes, change => change.MemberKind == "Form");
        Assert.Equal("Изменилась форма ФормаЭлемента", form.Summary);
    }

    [Fact]
    public void NestedFormItemKeepsPathAndDoesNotTouchTheGroup()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        before.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        after.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        before.Form("Catalogs/Номенклатура", "ФормаЭлемента", FormBody("""
                <UsualGroup name="Группа" id="2">
                  <ChildItems>
                    <InputField name="Артикул" id="5">
                      <Width>10</Width>
                    </InputField>
                  </ChildItems>
                </UsualGroup>
            """));
        after.Form("Catalogs/Номенклатура", "ФормаЭлемента", FormBody("""
                <UsualGroup name="Группа" id="2">
                  <ChildItems>
                    <InputField name="Артикул" id="5">
                      <Width>20</Width>
                    </InputField>
                  </ChildItems>
                </UsualGroup>
            """));

        var changes = ConfigComparer.Compare(before.Read(), after.Read());

        var item = Assert.Single(changes, change => change.MemberKind == "FormItem");
        Assert.Equal("ФормаЭлемента.Группа.Артикул", item.MemberName);
    }

    private static string FormBody(string items) => "  <Items>\n" + items + "\n  </Items>";

    private const string CatalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
}

public class CallChainTests
{
    [Fact]
    public void ChangedMethodOneLevelDeeperIsWarning()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        Chain(before, service: "Статус = 1;");
        Chain(after, service: "Статус = 2;");
        CallingExtension(extension);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal("цепочка вызовов", finding.Intervention);
        Assert.Equal("CommonModule.Сервис", finding.ObjectName);
        Assert.Equal("Пересчитать", finding.Method);
        Assert.Contains("Расчеты.Внешняя", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("Сервис.Пересчитать", finding.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void UnchangedChainGivesNothing()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        Chain(before, service: "Статус = 1;");
        Chain(after, service: "Статус = 1;");
        CallingExtension(extension);

        Assert.Empty(Impact(before, after, extension));
    }

    [Fact]
    public void DynamicCallInsideTheChainStopsTheGuess()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        Chain(before, service: "Статус = 1;", entryExtra: "Выполнить(\"Сообщить(1)\");");
        Chain(after, service: "Статус = 2;", entryExtra: "Выполнить(\"Сообщить(1)\");");
        CallingExtension(extension);

        Assert.Empty(Impact(before, after, extension));
    }

    [Fact]
    public void DirectCallWinsOverTheChain()
    {
        using var before = Dump.Create();
        using var after = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        Chain(before, service: "Статус = 1;");
        Chain(after, service: "Статус = 2;");
        extension.Catalog("Номенклатура", ExtensionCatalogId, [], extended: CatalogId);
        extension.Write("Catalogs/Номенклатура/Ext/ObjectModule.bsl", """
            Процедура ПриЗаписи(Отказ)
                Расчеты.Внешняя(Ссылка);
                Сервис.Пересчитать(Ссылка);
            КонецПроцедуры
            """);

        var finding = Assert.Single(Impact(before, after, extension));
        Assert.Equal("вызов", finding.Intervention);
        Assert.Equal("Пересчитать", finding.Method);
    }

    private static void Chain(Dump dump, string service, string entryExtra = "")
    {
        var extra = entryExtra.Length == 0 ? "" : "\n\t" + entryExtra;
        dump.CommonModule("Расчеты", CalculationsId, $"Процедура Внешняя(Док)\n\tСервис.Пересчитать(Док);{extra}\nКонецПроцедуры");
        dump.CommonModule("Сервис", ServiceId, $"Процедура Пересчитать(Док)\n\t{service}\nКонецПроцедуры");
        dump.Catalog("Номенклатура", CatalogId, []);
    }

    private static void CallingExtension(Dump extension)
    {
        extension.Catalog("Номенклатура", ExtensionCatalogId, [], extended: CatalogId);
        extension.Write("Catalogs/Номенклатура/Ext/ObjectModule.bsl", """
            Процедура ПриЗаписи(Отказ)
                Расчеты.Внешняя(Ссылка);
            КонецПроцедуры
            """);
    }

    private static IReadOnlyList<ImpactFinding> Impact(Dump before, Dump after, Dump extension)
    {
        var beforeDump = before.Read();
        var afterDump = after.Read();
        var changes = ConfigComparer.Compare(beforeDump, afterDump);
        return ExtensionAnalyzer.Analyze(beforeDump, afterDump, changes, [extension.Read()]);
    }

    private const string CalculationsId = "11111111-1111-1111-1111-111111111111";
    private const string ServiceId = "44444444-4444-4444-4444-444444444444";
    private const string CatalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string ExtensionCatalogId = "33333333-3333-3333-3333-333333333333";
}

public class RunHistoryTests
{
    [Fact]
    public void RunComparerSplitsRowsIntoNewGoneAndKept()
    {
        using var workspace = new Workspace();
        using var first = Dump.Create(version: "1.0.0.1");
        using var second = Dump.Create(version: "1.0.0.2");
        using var third = Dump.Create(version: "1.0.0.3");
        first.CommonModule("Расчеты", ModuleId, Methods(("НекотораяПроцедура", "Статус = 1;"), ("ДругаяПроцедура", "Сумма = 1;")));
        second.CommonModule("Расчеты", ModuleId, Methods(("НекотораяПроцедура", "Статус = 2;"), ("ДругаяПроцедура", "Сумма = 2;")));
        third.CommonModule("Расчеты", ModuleId, Methods(
            ("НекотораяПроцедура", "Статус = 2;"),
            ("ДругаяПроцедура", "Сумма = 1;"),
            ("ТретьяПроцедура", "Итог = 1;")));

        var previous = Dump.SaveRun(workspace.Folder("прогон1"), first, second);
        var current = Dump.SaveRun(workspace.Folder("прогон2"), first, third);
        var deltas = RunComparer.Compare(previous, current);

        Assert.Contains(deltas, delta => delta.Status == RunStatus.Kept && delta.Entry.Method == "НекотораяПроцедура");
        Assert.Contains(deltas, delta => delta.Status == RunStatus.Removed && delta.Entry.Method == "ДругаяПроцедура");
        Assert.Contains(deltas, delta => delta.Status == RunStatus.Added && delta.Entry.Method == "ТретьяПроцедура");
        Assert.All(deltas, delta => Assert.Equal(RunSection.Config, delta.Entry.Section));
        Assert.Equal("Новое", ReportWriter.RunStatusName(RunStatus.Added));
        Assert.Equal("Снятое", ReportWriter.RunStatusName(RunStatus.Removed));
        Assert.Equal("Осталось", ReportWriter.RunStatusName(RunStatus.Kept));
    }

    [Fact]
    public void RunComparerReadsImpactNextToConfig()
    {
        using var workspace = new Workspace();
        using var before = Dump.Create();
        using var afterQuiet = Dump.Create();
        using var afterBroken = Dump.Create();
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        afterQuiet.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        afterBroken.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ, Отказ)\n\tСтатус = 1;\nКонецПроцедуры");
        extension.ExtensionModule("Расш1", "Расчеты", ModuleId, """
            &После("НекотораяПроцедура")
            Процедура Расш1_НекотораяПроцедура(Документ)
            КонецПроцедуры
            """);

        var previous = Dump.SaveRun(workspace.Folder("прогон1"), before, afterQuiet, extension);
        var current = Dump.SaveRun(workspace.Folder("прогон2"), before, afterBroken, extension);
        var run = RunComparer.Load(current);
        var deltas = RunComparer.Compare(previous, current);

        Assert.True(run.HasImpact);
        var impact = Assert.Single(deltas, delta => delta.Entry.Section == RunSection.Impact);
        Assert.Equal(RunStatus.Added, impact.Status);
        Assert.Equal("Критично", impact.Entry.Kind);
        Assert.Equal("Расш1", impact.Entry.Extension);
        Assert.Equal("НекотораяПроцедура", impact.Entry.Method);
    }

    [Fact]
    public void RunComparerAcceptsConfigJsonItself()
    {
        using var workspace = new Workspace();
        using var before = Dump.Create(version: "1");
        using var after = Dump.Create(version: "2");
        before.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\n\tА = 1;\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура Пусто()\n\tА = 2;\nКонецПроцедуры");
        var previous = Dump.SaveRun(workspace.Folder("прогон1"), before, before);
        var current = Dump.SaveRun(workspace.Folder("прогон2"), before, after);

        var deltas = RunComparer.Compare(
            Path.Combine(previous, "config.json"),
            Path.Combine(current, "config.json"));

        Assert.Contains(deltas, delta => delta.Status == RunStatus.Added && delta.Entry.Kind == "Модуль (Тело)");
    }

    [Fact]
    public void RepeatedRunWritesTheSameReports()
    {
        using var workspace = new Workspace();
        using var before = Dump.Create(version: "1.0.0.1");
        using var after = Dump.Create(version: "1.0.0.2");
        using var extension = Dump.Create(name: "Расш1");
        before.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСервис.Пересчитать(Документ);\nКонецПроцедуры");
        after.CommonModule("Расчеты", ModuleId, "Процедура НекотораяПроцедура(Документ)\n\tСервис.Пересчитать(Документ);\nКонецПроцедуры");
        before.CommonModule("Сервис", ServiceId, "Процедура Пересчитать(Документ)\n\tСтатус = 1;\nКонецПроцедуры");
        after.CommonModule("Сервис", ServiceId, "Процедура Пересчитать(Документ)\n\tСтатус = 2;\nКонецПроцедуры");
        before.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        after.Catalog("Номенклатура", CatalogId, [], forms: ["ФормаЭлемента"]);
        before.Form("Catalogs/Номенклатура", "ФормаЭлемента", "  <Items>\n    <InputField name=\"Артикул\" id=\"5\" />\n  </Items>");
        after.Form("Catalogs/Номенклатура", "ФормаЭлемента", "  <Items>\n    <InputField name=\"Код\" id=\"6\" />\n  </Items>");
        extension.Catalog("Номенклатура", "33333333-3333-3333-3333-333333333333", [], extended: CatalogId);
        extension.Write("Catalogs/Номенклатура/Ext/ObjectModule.bsl", """
            Процедура ПриЗаписи(Отказ)
                Расчеты.НекотораяПроцедура(Ссылка);
            КонецПроцедуры
            """);

        var firstDirectory = workspace.Folder("прогон1");
        var secondDirectory = workspace.Folder("прогон2");
        Analysis.WriteReports(firstDirectory, Analysis.Run(before.Root, after.Root, extension.Root));
        Analysis.WriteReports(secondDirectory, Analysis.Run(before.Root, after.Root, extension.Root));

        Assert.Equal(
            File.ReadAllText(Path.Combine(firstDirectory, "config.json")),
            File.ReadAllText(Path.Combine(secondDirectory, "config.json")));
        Assert.Equal(
            File.ReadAllText(Path.Combine(firstDirectory, "impact.json")),
            File.ReadAllText(Path.Combine(secondDirectory, "impact.json")));
        Assert.DoesNotContain(
            RunComparer.Compare(firstDirectory, secondDirectory),
            delta => delta.Status != RunStatus.Kept);
        Assert.Contains(
            RunComparer.Load(firstDirectory).Entries,
            entry => entry.Section == RunSection.Impact && entry.Summary.Contains("Сервис.Пересчитать", StringComparison.Ordinal));
    }

    private static string Methods(params (string Name, string Body)[] methods) =>
        string.Join("\n\n", methods.Select(method => $"Процедура {method.Name}(Документ)\n\t{method.Body}\nКонецПроцедуры"));

    private const string ModuleId = "11111111-1111-1111-1111-111111111111";
    private const string ServiceId = "44444444-4444-4444-4444-444444444444";
    private const string CatalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
}

sealed class Workspace : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sverka-run-" + Guid.NewGuid().ToString("N"));

    public Workspace() => Directory.CreateDirectory(_root);

    public string Folder(string name)
    {
        var full = Path.Combine(_root, name);
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose() => Directory.Delete(_root, true);
}
