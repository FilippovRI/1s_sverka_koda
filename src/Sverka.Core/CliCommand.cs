namespace Sverka;

public static class CliCommand
{
    public static int Run(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h") || args.Length == 0)
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var beforePath = Require(args, "--before");
            var afterPath = Require(args, "--after");
            var extensionsPath = Optional(args, "--extensions");
            var outDir = Optional(args, "--out-dir") ?? Directory.GetCurrentDirectory();
            var result = Analysis.Run(beforePath, afterPath, extensionsPath);
            Directory.CreateDirectory(outDir);
            Analysis.WriteReports(outDir, result);

            Console.WriteLine($"Конфигурация: {result.Changes.Count} изменений");
            Console.WriteLine(Path.Combine(outDir, "config.json"));
            Console.WriteLine(Path.Combine(outDir, "config.html"));

            if (!result.ExtensionsChecked)
                return ExitCodes.Ok;

            var critical = result.Findings.Count(item => item.Severity == Severity.Critical);
            var adaptation = result.Findings.Count(item => item.Severity == Severity.Adaptation);
            var warning = result.Findings.Count(item => item.Severity == Severity.Warning);
            var uncheckedCount = result.Findings.Count(item => item.Severity == Severity.Unchecked);
            Console.WriteLine($"Расширения: критично {critical}, адаптация {adaptation}, предупреждение {warning}, не проверено {uncheckedCount}");
            Console.WriteLine(Path.Combine(outDir, "impact.json"));
            Console.WriteLine(Path.Combine(outDir, "impact.html"));
            return ExitCodes.FromFindings(result.Findings);
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or DirectoryNotFoundException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string Require(string[] args, string name)
    {
        var value = Optional(args, name);
        if (value == null)
            throw new ArgumentException($"Не указан параметр {name}.");
        return value;
    }

    private static string? Optional(string[] args, string name)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index + 1 >= args.Length)
                throw new ArgumentException($"У параметра {name} нет значения.");
            return args[index + 1];
        }

        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            sverka --before <каталог> --after <каталог> [--extensions <каталог>] [--out-dir <каталог>]

            Сравнивает две файловые выгрузки конфигурации 1С (Configuration.xml и модули .bsl).
            Без --extensions пишется только сверка конфигураций.
            С --extensions дополнительно пишется влияние на расширения.

            Код возврата относится к расширениям:
              0  нет критичных, адаптаций и предупреждений
              1  есть адаптация или предупреждение
              2  есть критичное
            «Не проверено» само по себе код не меняет.
            """);
    }
}
