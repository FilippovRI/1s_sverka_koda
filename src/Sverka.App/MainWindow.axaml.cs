using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Sverka;

namespace Sverka.App;

public partial class MainWindow : Window
{
    private AnalysisResult? _result;
    private TransferPlan? _transfer;
    private List<ChangeRow> _changes = [];
    private List<FindingRow> _findings = [];
    private List<SummaryRow> _summaries = [];
    private List<HistoryRow> _history = [];
    private List<TransferRow> _transfers = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnBrowseBefore(object? sender, RoutedEventArgs e) =>
        BeforeBox.Text = await PickFolder("Выгрузка ДО") ?? BeforeBox.Text;

    private async void OnBrowseAfter(object? sender, RoutedEventArgs e) =>
        AfterBox.Text = await PickFolder("Выгрузка ПОСЛЕ") ?? AfterBox.Text;

    private async void OnBrowseExtensions(object? sender, RoutedEventArgs e) =>
        ExtensionsBox.Text = await PickFolder("Расширения") ?? ExtensionsBox.Text;

    private void OnClearBefore(object? sender, RoutedEventArgs e) => BeforeBox.Text = "";

    private void OnClearAfter(object? sender, RoutedEventArgs e) => AfterBox.Text = "";

    private void OnClearExtensions(object? sender, RoutedEventArgs e) => ExtensionsBox.Text = "";

    private void OnShowHelp(object? sender, RoutedEventArgs e) => Pages.SelectedIndex = 0;

    private async void OnCompare(object? sender, RoutedEventArgs e)
    {
        var before = BeforeBox.Text?.Trim();
        var after = AfterBox.Text?.Trim();
        var extensions = ExtensionsBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(before) || string.IsNullOrWhiteSpace(after))
        {
            SetStatus("Нужны обе выгрузки: ДО и ПОСЛЕ.");
            return;
        }

        CompareButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        SetStatus("Сверяю выгрузки…");
        try
        {
            var result = await Task.Run(() => Analysis.Run(before, after, extensions));
            _result = result;
            _changes = result.Changes.Select(change => new ChangeRow(change)).ToList();
            _findings = result.Findings.Select(finding => new FindingRow(finding)).ToList();
            _summaries = result.Summaries.Select(summary => new SummaryRow(summary)).ToList();
            SummaryGrid.ItemsSource = _summaries;
            ApplyFilter();
            SaveButton.IsEnabled = true;
            Pages.SelectedIndex = 1;
            SetStatus(Describe(result));
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or DirectoryNotFoundException or ArgumentException)
        {
            SetStatus(exception.Message);
        }
        finally
        {
            CompareButton.IsEnabled = true;
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_result == null)
            return;

        var folder = await PickFolder("Куда сохранить отчёт");
        if (folder == null)
            return;

        try
        {
            Analysis.WriteReports(folder, _result);
            SetStatus($"Отчёт записан в {folder}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus(exception.Message);
        }
    }

    private async void OnBrowseTypical(object? sender, RoutedEventArgs e) =>
        TypicalBox.Text = await PickFolder("Типовая выгрузка") ?? TypicalBox.Text;

    private async void OnBrowseCustomized(object? sender, RoutedEventArgs e) =>
        CustomizedBox.Text = await PickFolder("Доработанная выгрузка") ?? CustomizedBox.Text;

    private void OnClearTypical(object? sender, RoutedEventArgs e) => TypicalBox.Text = "";

    private void OnClearCustomized(object? sender, RoutedEventArgs e) => CustomizedBox.Text = "";

    private async void OnFindTransfer(object? sender, RoutedEventArgs e)
    {
        var typical = TypicalBox.Text?.Trim();
        var customized = CustomizedBox.Text?.Trim();
        var name = TransferNameBox.Text?.Trim();
        var prefix = TransferPrefixBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typical) || string.IsNullOrWhiteSpace(customized))
        {
            SetTransferStatus("Нужны обе выгрузки: типовая и доработанная.");
            return;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(prefix))
        {
            SetTransferStatus("Укажите имя расширения и префикс.");
            return;
        }

        TransferFindButton.IsEnabled = false;
        TransferSaveButton.IsEnabled = false;
        SetTransferStatus("Ищу доработки…");
        try
        {
            var plan = await Task.Run(() => Analysis.RunTransfer(typical, customized, name, prefix));
            _transfer = plan;
            _transfers = plan.Items.Select(item => new TransferRow(item)).ToList();
            ApplyTransferFilter();
            TransferSaveButton.IsEnabled = true;
            SetTransferStatus(DescribeTransfer(plan));
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or DirectoryNotFoundException or ArgumentException)
        {
            _transfer = null;
            _transfers = [];
            ApplyTransferFilter();
            SetTransferStatus(exception.Message);
        }
        finally
        {
            TransferFindButton.IsEnabled = true;
        }
    }

    private async void OnSaveTransfer(object? sender, RoutedEventArgs e)
    {
        if (_transfer == null)
            return;

        var folder = await PickFolder("Куда сохранить черновик расширения");
        if (folder == null)
            return;

        try
        {
            Analysis.WriteTransferReports(folder, _transfer, writeDraft: true);
            SetTransferStatus($"Черновик записан в {folder} (extension/, transfer.html, transfer.json)");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetTransferStatus(exception.Message);
        }
    }

    private void OnTransferFilterChanged(object? sender, TextChangedEventArgs e) => ApplyTransferFilter();

    private async void OnBrowsePreviousRun(object? sender, RoutedEventArgs e) =>
        PreviousRunBox.Text = await PickFolder("Папка прошлого прогона") ?? PreviousRunBox.Text;

    private async void OnBrowseCurrentRun(object? sender, RoutedEventArgs e) =>
        CurrentRunBox.Text = await PickFolder("Папка текущего прогона") ?? CurrentRunBox.Text;

    private void OnClearPreviousRun(object? sender, RoutedEventArgs e) => PreviousRunBox.Text = "";

    private void OnClearCurrentRun(object? sender, RoutedEventArgs e) => CurrentRunBox.Text = "";

    private async void OnCompareRuns(object? sender, RoutedEventArgs e)
    {
        var previous = PreviousRunBox.Text?.Trim();
        var current = CurrentRunBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
        {
            SetHistoryStatus("Нужны обе папки: прошлый и текущий прогон.");
            return;
        }

        CompareRunsButton.IsEnabled = false;
        SetHistoryStatus("Сравниваю прогоны…");
        try
        {
            var deltas = await Task.Run(() => Analysis.CompareRuns(previous, current));
            _history = deltas.Select(delta => new HistoryRow(delta)).ToList();
            ApplyHistoryFilter();
            SetHistoryStatus(DescribeHistory(deltas));
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or DirectoryNotFoundException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            _history = [];
            ApplyHistoryFilter();
            SetHistoryStatus(exception.Message);
        }
        finally
        {
            CompareRunsButton.IsEnabled = true;
        }
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnHistoryFilterChanged(object? sender, TextChangedEventArgs e) => ApplyHistoryFilter();

    private void OnChangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ChangesGrid.SelectedItem is not ChangeRow row || _result == null)
        {
            BeforeText.Text = "";
            AfterText.Text = "";
            return;
        }

        BeforeText.Text = MethodText(_result.Before, row.Change);
        AfterText.Text = MethodText(_result.After, row.Change);
    }

    private void ApplyFilter()
    {
        if (ChangesGrid == null || FindingsGrid == null)
            return;

        var filter = FilterBox.Text?.Trim();
        ChangesGrid.ItemsSource = FilterRows(_changes, filter, row => $"{row.ObjectName} {row.Module} {row.Method} {row.Kind} {row.Summary}").ToList();
        FindingsGrid.ItemsSource = FilterRows(_findings, filter, row => $"{row.Extension} {row.ObjectName} {row.Method} {row.Intervention} {row.Severity} {row.Reason}").ToList();
    }

    private void ApplyHistoryFilter()
    {
        if (HistoryGrid == null)
            return;

        var filter = HistoryFilterBox.Text?.Trim();
        HistoryGrid.ItemsSource = FilterRows(
            _history,
            filter,
            row => $"{row.Status} {row.Section} {row.ObjectName} {row.Method} {row.Kind} {row.Summary}").ToList();
    }

    private void ApplyTransferFilter()
    {
        if (TransferGrid == null)
            return;

        var filter = TransferFilterBox.Text?.Trim();
        TransferGrid.ItemsSource = FilterRows(
            _transfers,
            filter,
            row => $"{row.ObjectName} {row.Method} {row.Kind} {row.Decision} {row.Hook} {row.Reason}").ToList();
    }

    private static IEnumerable<T> FilterRows<T>(IEnumerable<T> rows, string? filter, Func<T, string> text)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return rows;
        return rows.Where(row => text(row).Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> PickFolder(string title)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return null;

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void SetHistoryStatus(string text) => HistoryStatusText.Text = text;

    private void SetTransferStatus(string text) => TransferStatusText.Text = text;

    private static string DescribeTransfer(TransferPlan plan)
    {
        var auto = plan.Items.Count(item => item.Decision == TransferDecision.Auto);
        var manual = plan.Items.Count(item => item.Decision == TransferDecision.Manual);
        var highlight = plan.Items.Count(item => item.Decision == TransferDecision.Highlight);
        var skip = plan.Items.Count(item => item.Decision == TransferDecision.Skip);
        return $"Доработок: {plan.Items.Count}. Авто {auto}, вручную {manual}, подсветка {highlight}, пропуск {skip}.";
    }

    private static string DescribeHistory(IReadOnlyList<RunDelta> deltas)
    {
        var added = deltas.Count(item => item.Status == RunStatus.Added);
        var removed = deltas.Count(item => item.Status == RunStatus.Removed);
        var kept = deltas.Count(item => item.Status == RunStatus.Kept);
        return $"Новое {added}, снятое {removed}, осталось {kept}.";
    }

    private static string Describe(AnalysisResult result)
    {
        var text = $"Конфигурация: {result.Changes.Count} изменений.";
        if (!result.ExtensionsChecked)
            return text + " Расширения не переданы.";

        var critical = result.Findings.Count(item => item.Severity == Severity.Critical);
        var adaptation = result.Findings.Count(item => item.Severity == Severity.Adaptation);
        var warning = result.Findings.Count(item => item.Severity == Severity.Warning);
        var uncheckedCount = result.Findings.Count(item => item.Severity == Severity.Unchecked);
        return $"{text} Расширения: критично {critical}, адаптация {adaptation}, предупреждение {warning}, не проверено {uncheckedCount}.";
    }

    private static string MethodText(ConfigurationDump dump, ConfigChange change)
    {
        if (change.Method == null)
            return change.Summary;

        var owner = dump.Objects.FirstOrDefault(item =>
            (!string.IsNullOrEmpty(change.ObjectUuid) && item.Uuid.Equals(change.ObjectUuid, StringComparison.OrdinalIgnoreCase))
            || item.FullName.Equals(change.ObjectName, StringComparison.OrdinalIgnoreCase));
        var module = owner?.Modules.FirstOrDefault(item =>
            change.Module != null && item.RelativePath.Equals(change.Module, StringComparison.OrdinalIgnoreCase));
        var method = module?.Methods.FirstOrDefault(item =>
            item.Name.Equals(change.Method, StringComparison.OrdinalIgnoreCase));
        if (method == null || string.IsNullOrWhiteSpace(method.BodyText))
            return change.Summary;

        return method.BodyText.Trim();
    }
}

public sealed class ChangeRow
{
    public ChangeRow(ConfigChange change) => Change = change;

    public ConfigChange Change { get; }
    public string ObjectName => Change.ObjectName;
    public string Module => Change.Module ?? "";
    public string Method => Change.Method ?? Change.MemberName ?? "";
    public string Kind => ReportWriter.KindName(Change.Kind);
    public string Summary => Change.Summary;
}

public sealed class HistoryRow(RunDelta delta)
{
    public string Status => ReportWriter.RunStatusName(delta.Status);
    public string Section => ReportWriter.RunSectionName(delta.Entry.Section);
    public string ObjectName => delta.Entry.Extension == null
        ? delta.Entry.ObjectName
        : $"{delta.Entry.Extension}: {delta.Entry.ObjectName}";
    public string Method => delta.Entry.Target;
    public string Kind => delta.Entry.Kind;
    public string Summary => delta.Entry.Summary;
}

public sealed class FindingRow(ImpactFinding finding)
{
    public string Extension => finding.Extension;
    public string ObjectName => finding.ObjectName;
    public string Method => finding.Method ?? "";
    public string Intervention => finding.Intervention;
    public string Severity => ReportWriter.SeverityName(finding.Severity);
    public string Reason => finding.Reason;
}

public sealed class SummaryRow(ExtensionSummary summary)
{
    public string Extension => summary.Extension;
    public int Critical => summary.Critical;
    public int Adaptation => summary.Adaptation;
    public int Warning => summary.Warning;
    public int Unchecked => summary.Unchecked;
    public int Total => summary.Total;
}

public sealed class TransferRow(TransferItem item)
{
    public string ObjectName => item.ObjectName;
    public string Method => item.Method ?? "";
    public string Kind => ReportWriter.KindName(item.ChangeKind);
    public string Decision => ReportWriter.TransferDecisionName(item.Decision);
    public string Hook => ReportWriter.ProposedHookName(item.ProposedHook);
    public string Reason => item.Reason;
}
