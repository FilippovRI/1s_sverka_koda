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
    public void UnrelatedBodyChangeIsManual()
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
        Assert.Equal(TransferDecision.Manual, item.Decision);
        Assert.Equal(ProposedHook.None, item.ProposedHook);
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
    public void CosmeticSynonymIsSkipped()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Товары", catalogId, [new Attr("Артикул", "10", Synonym: "Артикул товара")]);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Skip, item.Decision);
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
    public void AddedAttributeIsHighlightOnlyWithoutXmlStub()
    {
        using var typical = Dump.Create();
        using var customized = Dump.Create();
        const string catalogId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        typical.Catalog("Товары", catalogId, [("Артикул", "10")]);
        customized.Catalog("Товары", catalogId, [("Артикул", "10"), ("СайтИд", "50")]);

        var plan = CustomizationFinder.Find(typical.Read(), customized.Read(), "Доработки", "Дораб_");
        var item = Assert.Single(plan.Items);
        Assert.Equal(TransferDecision.Highlight, item.Decision);
        Assert.Equal("СайтИд", item.Method);

        var directory = Path.Combine(Path.GetTempPath(), "sverka-transfer-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtensionDraftWriter.Write(directory, plan);
            Assert.True(File.Exists(Path.Combine(directory, "Configuration.xml")));
            Assert.False(Directory.Exists(Path.Combine(directory, "Catalogs")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
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

        var directory = Path.Combine(Path.GetTempPath(), "sverka-transfer-" + Guid.NewGuid().ToString("N"));
        try
        {
            ExtensionDraftWriter.Write(directory, plan);
            var module = File.ReadAllText(
                Path.Combine(directory, "CommonModules", "Расчеты", "Ext", "Module.bsl"),
                Encoding.UTF8);
            Assert.Contains("&ИзменениеИКонтроль(\"НекотораяПроцедура\")", module, StringComparison.Ordinal);
            Assert.Contains("#Вставка", module, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
