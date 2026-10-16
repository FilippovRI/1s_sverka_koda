using System.Text;

namespace Sverka.Core.Tests;

public class TransferTests
{
    private const string ModuleId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void TailCustomizationBecomesAfterHook()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Доработка = Истина;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items.Where(entry => entry.Decision != TransferDecision.Skip));
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.After, item.ProposedHook);
        Assert.Contains("Доработка = Истина", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadCustomizationBecomesBeforeHook()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Подготовка = Истина;
            	Статус = 1;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.Before, item.ProposedHook);
        Assert.Contains("Подготовка = Истина", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void UnrelatedBodyChangeBecomesInstead()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Итог = 2;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	СовсемДругое = 9;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.Instead, item.ProposedHook);
        Assert.Contains("СовсемДругое = 9", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void BslHighlighterMarksKeywordsAndDirectives()
    {
        var tokens = BslHighlighter.Tokenize("+ Процедура Тест() // коммент");
        Assert.Contains(tokens, token => token.Kind == BslHighlighter.TokenKind.DiffMarker);
        Assert.Contains(tokens, token => token.Kind == BslHighlighter.TokenKind.Keyword && token.Text == "Процедура");
        Assert.Contains(tokens, token => token.Kind == BslHighlighter.TokenKind.Comment);
    }

    [Fact]
    public void CoverageMarksAutoAsCoveredAfterDraftWrite()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Доработка = Истина;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var dir = Path.Combine(Path.GetTempPath(), "sverka-cov-" + Guid.NewGuid().ToString("N"));
        try
        {
            Analysis.WriteTransferReports(dir, plan, writeDraft: true);
            var report = CoverageChecker.Analyze(plan, Path.Combine(dir, "extension"));
            Assert.True(report.Covered >= 1);
            Assert.Equal(0, report.Missing);
            Assert.True(File.Exists(Path.Combine(dir, "coverage.html")));
            Assert.True(File.Exists(Path.Combine(dir, "transfer-session.json")));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TransferSessionRoundTripsWorkStatus()
    {
        var session = new TransferSession { ExtensionName = "Доработки", Prefix = "Дораб_" };
        session.SetStatus("CommonModule.А|Ext/Module.bsl|М|", TransferWorkStatus.Done);
        var path = Path.Combine(Path.GetTempPath(), "sverka-session-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            TransferSessionStore.Save(path, session);
            var loaded = TransferSessionStore.TryLoad(path);
            Assert.NotNull(loaded);
            Assert.Equal(TransferWorkStatus.Done, loaded!.GetStatus("CommonModule.А|Ext/Module.bsl|М|"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LineReplacementBecomesChangeAndControlWithDelete()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Итог = 2;
            	Финиш = 3;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Итог = 99;
            	Финиш = 3;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.ChangeAndControl, item.ProposedHook);
        Assert.Contains("#Удаление", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("#Вставка", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("Итог = 2", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("Итог = 99", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void AddedMethodIsAutoWithPrefixInDraft()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры

            Процедура СвояПроцедура(Документ)
            	Значение = 5;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items, entry => entry.Method == "СвояПроцедура");
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.None, item.ProposedHook);

        var directory = Path.Combine(Path.GetTempPath(), "sverka-transfer-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtensionDraftWriter.Write(directory, plan);
            Assert.True(File.Exists(Path.Combine(directory, "Configuration.xml")));
            var module = File.ReadAllText(
                Path.Combine(directory, "CommonModules", "Расчеты", "Ext", "Module.bsl"),
                Encoding.UTF8);
            Assert.Contains("Дораб_СвояПроцедура", module, StringComparison.Ordinal);
            Assert.Contains("Значение = 5", module, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CosmeticSynonymIsNotListed()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Товары", catalogId, [new Attr("Артикул", "10", Synonym: "Артикул товара")]);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void ProceedWithCallBecomesInsteadHook()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Доработка = Истина;
            	ПродолжитьВызов();
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.Instead, item.ProposedHook);
        Assert.Contains("Доработка = Истина", item.HookBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ПродолжитьВызов", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void AddedAttributeIsExcludedFromTransferRegistry()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Товары", catalogId, [("Артикул", "10"), ("СайтИд", "50")]);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        Assert.DoesNotContain(plan.Items, item => item.Method == "СайтИд");
    }

    [Fact]
    public void AddedTemplateIsExcludedFromTransferRegistry()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Write("Catalogs/Товары.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <MetaDataObject>
              <Catalog uuid="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa">
                <Properties>
                  <Name>Товары</Name>
                </Properties>
                <ChildObjects>
                  <Attribute uuid="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb0">
                    <Properties>
                      <Name>Артикул</Name>
                      <Type>
                        <Length>10</Length>
                      </Type>
                    </Properties>
                  </Attribute>
                  <Template>ПечатнаяФорма</Template>
                </ChildObjects>
              </Catalog>
            </MetaDataObject>
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        Assert.DoesNotContain(plan.Items, item => item.Method == "ПечатнаяФорма");
    }

    [Fact]
    public void ObjectTypeFilterGroupsKnownTypes()
    {
        var groups = ObjectTypeFilter.Build([
            "CommonModule.А",
            "Document.Б",
            "Catalog.В",
            "DataProcessor.Г",
            "InformationRegister.Д",
        ]);
        Assert.Contains(groups, group => group.TypeKey == "CommonModule" && group.ObjectNames.Contains("CommonModule.А"));
        Assert.Contains(groups, group => group.TypeKey == "Document");
        Assert.Contains(groups, group => group.TypeKey == "DataProcessor");
        Assert.DoesNotContain(groups, group => group.TypeKey == "Catalog");
    }

    [Fact]
    public void TextDiffMarksInsertedAndDeletedLines()
    {
        var (left, right) = TextDiff.MarkSides(
            "Статус = 1;\nСтарое = 9;\nИтог = 2;",
            "Статус = 1;\nДоработка = Истина;\nИтог = 2;");
        Assert.Contains("- Старое = 9;", left, StringComparison.Ordinal);
        Assert.Contains("+ Доработка = Истина;", right, StringComparison.Ordinal);
        Assert.Contains("  Статус = 1;", left, StringComparison.Ordinal);
        Assert.Contains("  Итог = 2;", right, StringComparison.Ordinal);
    }

    [Fact]
    public void NewObjectIsHighlightOnly()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string extraId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Сайты", extraId, [("Код", "10")]);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items, entry => entry.ObjectName.Contains("Сайты", StringComparison.Ordinal));
        Assert.Equal(TransferDecision.Highlight, item.Decision);
        Assert.Contains("подсветка", item.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemovedMetadataHiddenByDefaultButNewObjectsStay()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string keepId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string removedId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
        const string addedId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        typical.Catalog("Товары", keepId, [("Артикул", "10"), ("Старый", "10")]);
        typical.Catalog("Удалённый", removedId, [("Код", "10")]);
        customized.Catalog("Товары", keepId, [("Артикул", "10")]);
        customized.Catalog("Сайты", addedId, [("Код", "10")]);

        var hidden = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        Assert.DoesNotContain(hidden.Items, item => item.ChangeKind == ChangeKind.Removed);
        Assert.DoesNotContain(hidden.Items, item =>
            item.Method == "Старый" && item.Decision != TransferDecision.Skip);
        Assert.Contains(hidden.Items, item => item.ObjectName.Contains("Сайты", StringComparison.Ordinal));

        var shown = CustomizationFinder.Find(
            typical.Read(), customized.Read(), "Доработки", "Дораб_",
            includeRemovedMetadata: true);
        Assert.Contains(shown.Items, item => item.ChangeKind == ChangeKind.Removed);
    }

    [Fact]
    public void ListFormChangesAreNotListed()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")], forms: ["ФормаСписка"]);
        customized.Catalog("Товары", catalogId, [("Артикул", "10")], forms: ["ФормаСписка"]);
        typical.Form("Catalogs/Товары", "ФормаСписка", """
            <Items>
              <LabelField name="СтароеПоле"/>
            </Items>
            """);
        customized.Form("Catalogs/Товары", "ФормаСписка", """
            <Items>
              <LabelField name="НовоеПоле"/>
            </Items>
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        Assert.DoesNotContain(plan.Items, item =>
            item.MemberKind != null && item.MemberKind.Contains("Form", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SingleInsertBecomesChangeAndControl()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Итог = 2;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Доработка = Истина;
            	Итог = 2;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.ChangeAndControl, item.ProposedHook);
        Assert.Contains("Доработка = Истина", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("#Вставка", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("#КонецВставки", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("Статус = 1", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("Итог = 2", item.HookBody, StringComparison.Ordinal);

        var directory = Path.Combine(Path.GetTempPath(), "sverka-transfer-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtensionDraftWriter.Write(directory, plan);
            var module = File.ReadAllText(
                Path.Combine(directory, "CommonModules", "Расчеты", "Ext", "Module.bsl"),
                Encoding.UTF8);
            Assert.Contains("&ИзменениеИКонтроль(\"НекотораяПроцедура\")", module, StringComparison.Ordinal);
            Assert.Contains("#Вставка", module, StringComparison.Ordinal);
            Assert.Contains("Статус = 1", module, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MultipleInsertsBecomeChangeAndControl()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        typical.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	Итог = 2;
            	Финиш = 3;
            КонецПроцедуры
            """);
        customized.CommonModule("Расчеты", ModuleId, """
            Процедура НекотораяПроцедура(Документ)
            	Статус = 1;
            	ВставкаА = Истина;
            	Итог = 2;
            	ВставкаБ = Истина;
            	Финиш = 3;
            КонецПроцедуры
            """);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.ChangeAndControl, item.ProposedHook);
        Assert.Contains("#Вставка", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("ВставкаА", item.HookBody, StringComparison.Ordinal);
        Assert.Contains("ВставкаБ", item.HookBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SubsystemScopeFiltersObjects()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string salesModule = "11111111-1111-1111-1111-111111111111";
        const string stockModule = "22222222-2222-2222-2222-222222222222";
        const string salesSub = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.CommonModule("ПродажиСервер", salesModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        typical.CommonModule("СкладСервер", stockModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("ПродажиСервер", salesModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            	ДоработкаПродаж = Истина;
            КонецПроцедуры
            """);
        customized.CommonModule("СкладСервер", stockModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            	ДоработкаСклада = Истина;
            КонецПроцедуры
            """);
        typical.Subsystem("Продажи", salesSub, ["CommonModule.ПродажиСервер"]);
        customized.Subsystem("Продажи", salesSub, ["CommonModule.ПродажиСервер"]);

        var tree = SubsystemCatalog.Merge(
            SubsystemCatalog.Read(typical.Root),
            SubsystemCatalog.Read(customized.Root));
        var scope = Analysis.BuildScope(tree, ["Продажи"], includeDescendants: true);
        Assert.Contains("CommonModule.ПродажиСервер", scope.ObjectNames);
        Assert.DoesNotContain("CommonModule.СкладСервер", scope.ObjectNames);

        var plan = CustomizationFinder.Find(
            typical.Read(),
            customized.Read(),
            "Доработки",
            "Дораб_",
            scope,
            tree);
        var item = Assert.Single(plan.Items.Where(entry => entry.Decision != TransferDecision.Skip));
        Assert.Equal("CommonModule.ПродажиСервер", item.ObjectName);
        Assert.Equal(TransferDecision.Auto, item.Decision);
        Assert.Equal(ProposedHook.After, item.ProposedHook);
    }

    [Fact]
    public void ExtraObjectAddedToScopeWithoutSubsystem()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string stockModule = "22222222-2222-2222-2222-222222222222";
        typical.CommonModule("СкладСервер", stockModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("СкладСервер", stockModule, """
            Процедура Провести(Документ)
            	Статус = 1;
            	ДоработкаСклада = Истина;
            КонецПроцедуры
            """);

        var scope = Analysis.BuildScope([], [], includeDescendants: true, ["CommonModule.СкладСервер"]);
        Assert.True(scope.HasFilter);
        Assert.Contains("CommonModule.СкладСервер", scope.ExtraObjectNames);
        Assert.Contains("CommonModule.СкладСервер", scope.ObjectNames);

        var plan = CustomizationFinder.Find(
            typical.Read(), customized.Read(), "Доработки", "Дораб_", scope, []);
        var item = Assert.Single(plan.Items);
        Assert.Equal("CommonModule.СкладСервер", item.ObjectName);
        Assert.Equal(TransferDecision.Auto, item.Decision);
    }

    [Fact]
    public void SubsystemsJsonRoundTripKeepsExtras()
    {
        var scope = Analysis.BuildScope(
            [],
            ["Продажи"],
            includeDescendants: true,
            ["CommonModule.СкладСервер"]);
        var path = Path.Combine(Path.GetTempPath(), "sverka-scope-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            SubsystemCatalog.SaveSelection(path, scope);
            var loaded = SubsystemCatalog.TryLoadSelection(path);
            Assert.NotNull(loaded);
            Assert.Contains("Продажи", loaded!.SelectedSubsystemNames);
            Assert.Contains("CommonModule.СкладСервер", loaded.ExtraObjectNames);
            Assert.True(loaded.IncludeDescendants);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void NestedSubsystemIncludedWhenDescendantsEnabled()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string moduleId = "11111111-1111-1111-1111-111111111111";
        const string parentId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string childId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        typical.CommonModule("ОптСервер", moduleId, """
            Процедура Провести(Документ)
            	Статус = 1;
            КонецПроцедуры
            """);
        customized.CommonModule("ОптСервер", moduleId, """
            Процедура Провести(Документ)
            	Статус = 1;
            	Хвост = 1;
            КонецПроцедуры
            """);
        typical.Subsystem("Продажи", parentId, []);
        typical.Subsystem("Опт", childId, ["CommonModule.ОптСервер"], parentPath: "Subsystems/Продажи");
        customized.Subsystem("Продажи", parentId, []);
        customized.Subsystem("Опт", childId, ["CommonModule.ОптСервер"], parentPath: "Subsystems/Продажи");

        var tree = SubsystemCatalog.Merge(
            SubsystemCatalog.Read(typical.Root),
            SubsystemCatalog.Read(customized.Root));
        Assert.Contains(SubsystemCatalog.Flatten(tree), node => node.FullName == "Продажи.Опт");

        var scope = Analysis.BuildScope(tree, ["Продажи"], includeDescendants: true);
        Assert.Contains("CommonModule.ОптСервер", scope.ObjectNames);

        var plan = CustomizationFinder.Find(
            typical.Read(), customized.Read(), "Доработки", "Дораб_", scope, tree);
        Assert.Contains(plan.Items, item => item.ObjectName == "CommonModule.ОптСервер");
    }
}
