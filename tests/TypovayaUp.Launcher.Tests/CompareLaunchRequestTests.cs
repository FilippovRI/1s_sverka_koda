using TypovayaUp.Launcher;

namespace TypovayaUp.Launcher.Tests;

public sealed class CompareLaunchRequestTests
{
    static CompareLaunchRequest Base() => new()
    {
        OscriptExecutable = @"C:\Program Files\OneScript\bin\oscript.exe",
        MainScriptPath = @"C:\repo\typovaya-up\src\Главный.os",
        SourceDumpPath = @"D:\Выгрузки\Исходная УП",
        VendorDumpPath = @"D:\Выгрузки\Типовая БП",
        OutputDirectory = @"D:\out\результат",
    };

    [Fact]
    public void BuildArgumentList_QuotedPaths_SeparateArgv()
    {
        var req = Base();
        var args = req.BuildArgumentList();
        Assert.Contains("--исходная", args);
        var i = args.IndexOf("--исходная");
        Assert.Equal(@"D:\Выгрузки\Исходная УП", args[i + 1]);
        Assert.Equal(@"D:\Выгрузки\Типовая БП", args[args.IndexOf("--поставщик") + 1]);
    }

    [Fact]
    public void BuildArgumentList_FormsPreview_OnlyPreviewFlag()
    {
        var req = Base() with { FormsPreview = true, AnalyzeForms = false };
        var args = req.BuildArgumentList();
        Assert.Contains("--превью-форм", args);
        Assert.DoesNotContain("--анализ-форм", args);
    }

    [Fact]
    public void BuildArgumentList_AnalyzeForms_WhenNoPreview()
    {
        var req = Base() with { AnalyzeForms = true, FormsPreview = false };
        var args = req.BuildArgumentList();
        Assert.Contains("--анализ-форм", args);
    }
}
