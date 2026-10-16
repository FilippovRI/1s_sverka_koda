using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Sverka;

namespace Sverka.App;

public partial class MainWindow : Window
{
    private AnalysisResult? _result;
    private TransferPlan? _transfer;
    private TransferSession _session = new();
    private string? _lastDraftFolder;
    private List<ChangeRow> _changes = [];
    private List<FindingRow> _findings = [];
    private List<SummaryRow> _summaries = [];
    private List<HistoryRow> _history = [];
    private List<TransferRow> _transfers = [];
    private ObservableCollection<SubsystemTreeNode> _subsystemRoots = [];
    private ObservableCollection<SubsystemTreeNode> _objectFilterRoots = [];
    private readonly ObservableCollection<string> _extraObjects = [];
    private IReadOnlyList<SubsystemInfo> _subsystemTree = [];
    private CancellationTokenSource? _objectFilterRebuildCts;
    private CancellationTokenSource? _transferRescanCts;
    private bool _suppressObjectFilterRebuild;
    private bool _suppressSubsystemSync;
    private bool _transferScanRunning;

    public MainWindow()
    {
        InitializeComponent();
        ExtraObjectsList.ItemsSource = _extraObjects;
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

    private void OnShowHelp(object? sender, RoutedEventArgs e)
    {
        MainPages.SelectedIndex = 0;
        ComparePages.SelectedIndex = 0;
    }

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
            MainPages.SelectedIndex = 0;
            ComparePages.SelectedIndex = 1;
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
        TypicalBox.Text = await PickFolder("У поставщика") ?? TypicalBox.Text;

    private async void OnBrowseCustomized(object? sender, RoutedEventArgs e) =>
        CustomizedBox.Text = await PickFolder("В доработанной") ?? CustomizedBox.Text;

    private void OnClearTypical(object? sender, RoutedEventArgs e) => TypicalBox.Text = "";

    private void OnClearCustomized(object? sender, RoutedEventArgs e) => CustomizedBox.Text = "";

    private async void OnLoadSubsystems(object? sender, RoutedEventArgs e)
    {
        var typical = TypicalBox.Text?.Trim();
        var customized = CustomizedBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typical) || string.IsNullOrWhiteSpace(customized))
        {
            SubsystemStatusText.Text = "Сначала укажите типовую и доработанную выгрузки.";
            return;
        }

        try
        {
            var previously = CollectSelectedSubsystemNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tree = await Task.Run(() => Analysis.LoadSubsystems(typical, customized));
            BindSubsystemTree(tree, previously.Count == 0 ? null : previously);
            SubsystemStatusText.Text = _subsystemRoots.Count == 0
                ? "В выгрузках нет подсистем — сравнение по всей конфигурации (можно добавить объекты вручную)."
                : $"Подсистем: {SubsystemTreeNode.Flatten(_subsystemRoots).Count()}. Отметьте нужные или загрузите subsystems.json.";
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or DirectoryNotFoundException or ArgumentException or IOException)
        {
            SubsystemStatusText.Text = exception.Message;
        }
    }

    private async void OnLoadScopeFile(object? sender, RoutedEventArgs e)
    {
        var path = await PickJsonFile("Выберите subsystems.json");
        if (path == null)
            return;

        try
        {
            var saved = SubsystemCatalog.TryLoadSelection(path);
            if (saved == null)
            {
                SubsystemStatusText.Text = "Не удалось прочитать subsystems.json.";
                return;
            }

            var typical = TypicalBox.Text?.Trim();
            var customized = CustomizedBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(typical) && !string.IsNullOrWhiteSpace(customized) && _subsystemRoots.Count == 0)
            {
                var tree = await Task.Run(() => Analysis.LoadSubsystems(typical, customized));
                BindSubsystemTree(tree, saved.SelectedSubsystemNames.ToHashSet(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                ApplySubsystemSelection(saved.SelectedSubsystemNames);
            }

            IncludeDescendantsBox.IsChecked = saved.IncludeDescendants;
            _extraObjects.Clear();
            foreach (var name in saved.ExtraObjectNames)
                _extraObjects.Add(name);
            RebuildObjectFilterTree();
            SubsystemStatusText.Text =
                $"Восстановлено: {saved.SelectedSubsystemNames.Count} подсистем, {_extraObjects.Count} доп. объектов.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            SubsystemStatusText.Text = exception.Message;
        }
    }

    private void OnSelectAllSubsystems(object? sender, RoutedEventArgs e)
    {
        _suppressObjectFilterRebuild = true;
        try
        {
            foreach (var root in _subsystemRoots)
                root.IsChecked = true;
        }
        finally
        {
            _suppressObjectFilterRebuild = false;
        }

        RebuildObjectFilterTree();
        ScheduleTransferRescan();
    }

    private void OnClearSubsystems(object? sender, RoutedEventArgs e)
    {
        _suppressObjectFilterRebuild = true;
        try
        {
            foreach (var root in _subsystemRoots)
                root.IsChecked = false;
        }
        finally
        {
            _suppressObjectFilterRebuild = false;
        }

        RebuildObjectFilterTree();
        ScheduleTransferRescan();
    }

    private void OnAddExtraObject(object? sender, RoutedEventArgs e)
    {
        var name = ExtraObjectBox.Text?.Trim() ?? "";
        if (name.Length == 0)
            return;
        if (!_extraObjects.Contains(name, StringComparer.OrdinalIgnoreCase))
            _extraObjects.Add(name);
        ExtraObjectBox.Text = "";
        RebuildObjectFilterTree();
    }

    private void OnRemoveExtraObject(object? sender, RoutedEventArgs e)
    {
        if (ExtraObjectsList.SelectedItem is string selected)
            _extraObjects.Remove(selected);
        else if (!string.IsNullOrWhiteSpace(ExtraObjectBox.Text))
        {
            var name = ExtraObjectBox.Text.Trim();
            var match = _extraObjects.FirstOrDefault(item => item.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                _extraObjects.Remove(match);
        }

        RebuildObjectFilterTree();
    }

    private async void OnFindTransfer(object? sender, RoutedEventArgs e) =>
        await RunTransferScanAsync(showBusyInStatus: true);

    private async Task RunTransferScanAsync(bool showBusyInStatus)
    {
        var typical = TypicalBox.Text?.Trim();
        var customized = CustomizedBox.Text?.Trim();
        var name = TransferNameBox.Text?.Trim();
        var prefix = TransferPrefixBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typical) || string.IsNullOrWhiteSpace(customized))
        {
            if (showBusyInStatus)
            {
                TransferSettingsExpander.IsExpanded = true;
                SetTransferStatus("Нужны обе выгрузки: типовая и доработанная — разверните блок выше.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(prefix))
        {
            if (showBusyInStatus)
            {
                TransferSettingsExpander.IsExpanded = true;
                SetTransferStatus("Укажите имя расширения и префикс — разверните блок выше.");
            }

            return;
        }

        if (_transferScanRunning)
            return;
        _transferScanRunning = true;
        if (TransferFindButton != null)
            TransferFindButton.IsEnabled = false;
        SetTransferSaveEnabled(false);
        if (showBusyInStatus)
            SetTransferStatus("Ищу доработки…");
        try
        {
            if (_subsystemTree.Count == 0 && Directory.Exists(Path.Combine(typical, "Subsystems")))
            {
                _subsystemTree = await Task.Run(() => Analysis.LoadSubsystems(typical, customized));
                if (_subsystemRoots.Count == 0)
                    BindSubsystemTree(_subsystemTree, selected: null);
            }

            if (_objectFilterRoots.Count == 0)
                RebuildObjectFilterTree();

            var includeRemoved = ShowRemovedMetadataBox.IsChecked == true;
            var scope = BuildCurrentScope();

            var plan = await Task.Run(() =>
                Analysis.RunTransfer(typical, customized, name, prefix, scope, includeRemoved));
            _transfer = plan;
            _session.ExtensionName = name!;
            _session.Prefix = prefix!;
            _session.TypicalPath = typical;
            _session.CustomizedPath = customized;
            _transfers = plan.Items
                .OrderBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Method ?? "", StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    var row = new TransferRow(item);
                    row.SetWorkStatus(_session.GetStatus(row.ItemKey), notify: false);
                    return row;
                })
                .ToList();
            ApplyTransferFilter();
            SetTransferSaveEnabled(true);
            if (TransferCoverageButton != null)
                TransferCoverageButton.IsEnabled = !string.IsNullOrEmpty(_lastDraftFolder);
            SetTransferStatus(DescribeTransfer(plan) + " " + DescribeSessionProgress());
            if (scope.HasFilter && SubsystemStatusText != null)
                SubsystemStatusText.Text =
                    $"Область: {scope.SelectedSubsystemNames.Count} подсистем, объектов {scope.ObjectNames.Count}.";
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
            _transferScanRunning = false;
            if (TransferFindButton != null)
                TransferFindButton.IsEnabled = true;
        }
    }

    private void BindSubsystemTree(IReadOnlyList<SubsystemInfo> tree, HashSet<string>? selected)
    {
        _subsystemTree = tree;
        DetachSubsystemCheckHandlers(_subsystemRoots);
        _subsystemRoots = new ObservableCollection<SubsystemTreeNode>(
            tree.Select(SubsystemTreeNode.FromInfo));
        SubsystemTree.ItemsSource = _subsystemRoots;
        AttachSubsystemCheckHandlers(_subsystemRoots);
        if (selected != null && selected.Count > 0)
            ApplySubsystemSelection(selected);
        RebuildObjectFilterTree();
    }

    private void AttachSubsystemCheckHandlers(IEnumerable<SubsystemTreeNode> roots)
    {
        foreach (var node in SubsystemTreeNode.Flatten(roots))
            node.PropertyChanged += OnSubsystemNodePropertyChanged;
    }

    private void DetachSubsystemCheckHandlers(IEnumerable<SubsystemTreeNode> roots)
    {
        foreach (var node in SubsystemTreeNode.Flatten(roots))
            node.PropertyChanged -= OnSubsystemNodePropertyChanged;
    }

    private void OnSubsystemNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SubsystemTreeNode.IsChecked) || _suppressObjectFilterRebuild)
            return;
        ScheduleObjectFilterRebuild();
    }

    private void OnObjectFilterNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SubsystemTreeNode.IsChecked) || _suppressSubsystemSync)
            return;
        ScheduleSubsystemSyncFromObjects();
    }

    private void ScheduleObjectFilterRebuild()
    {
        _objectFilterRebuildCts?.Cancel();
        _objectFilterRebuildCts = new CancellationTokenSource();
        var token = _objectFilterRebuildCts.Token;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested)
                    return;
                RebuildObjectFilterTree();
                ScheduleTransferRescan();
            }
            catch (TaskCanceledException)
            {
                // debounce
            }
        });
    }

    private void ScheduleSubsystemSyncFromObjects()
    {
        _objectFilterRebuildCts?.Cancel();
        _objectFilterRebuildCts = new CancellationTokenSource();
        var token = _objectFilterRebuildCts.Token;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested)
                    return;
                SyncSubsystemsFromSelectedObjects();
                ScheduleTransferRescan();
            }
            catch (TaskCanceledException)
            {
                // debounce
            }
        });
    }

    /// <summary>
    /// Подсистемы отражают выбранные объекты: отмечена, если выбраны все её объекты из фильтра;
    /// частично — если часть; снята — если ни одного.
    /// </summary>
    private void SyncSubsystemsFromSelectedObjects()
    {
        if (_subsystemRoots.Count == 0 || _objectFilterRoots.Count == 0)
            return;

        var selectedObjects = CollectSelectedObjectNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includeDescendants = IncludeDescendantsBox.IsChecked != false;
        _suppressObjectFilterRebuild = true;
        try
        {
            foreach (var node in SubsystemTreeNode.Flatten(_subsystemRoots))
            {
                var content = ObjectsOfSubsystem(node.FullName, includeDescendants)
                    .Where(ObjectTypeFilter.IsKnownType)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (content.Count == 0)
                {
                    node.SetCheckedSilent(false);
                    continue;
                }

                var selectedCount = content.Count(selectedObjects.Contains);
                if (selectedCount == 0)
                    node.SetCheckedSilent(false);
                else if (selectedCount == content.Count)
                    node.SetCheckedSilent(true);
                else
                    node.SetCheckedSilent(null);
            }
        }
        finally
        {
            _suppressObjectFilterRebuild = false;
        }
    }

    private IEnumerable<string> ObjectsOfSubsystem(string subsystemFullName, bool includeDescendants)
    {
        foreach (var node in SubsystemCatalog.Flatten(_subsystemTree))
        {
            if (!node.FullName.Equals(subsystemFullName, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var item in node.Content)
                yield return item;
            if (!includeDescendants)
                yield break;
            foreach (var child in SubsystemCatalog.Flatten(node.Children))
            {
                foreach (var item in child.Content)
                    yield return item;
            }

            yield break;
        }
    }

    /// <summary>После смены области — тихо пересчитать реестр, если пути уже заданы.</summary>
    private void ScheduleTransferRescan()
    {
        var typical = TypicalBox.Text?.Trim();
        var customized = CustomizedBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typical) || string.IsNullOrWhiteSpace(customized))
            return;
        if (string.IsNullOrWhiteSpace(TransferNameBox.Text) || string.IsNullOrWhiteSpace(TransferPrefixBox.Text))
            return;

        _transferRescanCts?.Cancel();
        _transferRescanCts = new CancellationTokenSource();
        var token = _transferRescanCts.Token;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await Task.Delay(450, token);
                if (token.IsCancellationRequested || _transferScanRunning)
                    return;
                await RunTransferScanAsync(showBusyInStatus: true);
            }
            catch (TaskCanceledException)
            {
                // debounce
            }
        });
    }

    private void ApplySubsystemSelection(IEnumerable<string> selectedNames)
    {
        _suppressObjectFilterRebuild = true;
        try
        {
            foreach (var node in SubsystemTreeNode.Flatten(_subsystemRoots))
                node.SetCheckedSilent(false);

            var selected = selectedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var node in SubsystemTreeNode.Flatten(_subsystemRoots))
            {
                if (selected.Contains(node.FullName))
                    node.IsChecked = true; // каскад на детей
            }
        }
        finally
        {
            _suppressObjectFilterRebuild = false;
        }

        RebuildObjectFilterTree();
    }

    private List<string> CollectSelectedSubsystemNames() =>
        SubsystemTreeNode.Flatten(_subsystemRoots)
            .Where(node => node.IsChecked == true)
            .Select(node => node.FullName)
            .ToList();

    private void OnRebuildObjectFilter(object? sender, RoutedEventArgs e) => RebuildObjectFilterTree();

    private void OnIncludeDescendantsChanged(object? sender, RoutedEventArgs e)
    {
        RebuildObjectFilterTree();
        ScheduleTransferRescan();
    }

    private void OnSelectAllObjects(object? sender, RoutedEventArgs e)
    {
        _suppressSubsystemSync = true;
        try
        {
            foreach (var root in _objectFilterRoots)
                root.IsChecked = true;
        }
        finally
        {
            _suppressSubsystemSync = false;
        }

        SyncSubsystemsFromSelectedObjects();
        ScheduleTransferRescan();
    }

    private void OnClearObjects(object? sender, RoutedEventArgs e)
    {
        _suppressSubsystemSync = true;
        try
        {
            foreach (var root in _objectFilterRoots)
                root.IsChecked = false;
        }
        finally
        {
            _suppressSubsystemSync = false;
        }

        SyncSubsystemsFromSelectedObjects();
        ScheduleTransferRescan();
    }

    private void RebuildObjectFilterTree()
    {
        if (ObjectFilterTree == null)
            return;

        DetachObjectFilterHandlers(_objectFilterRoots);

        var includeDescendants = IncludeDescendantsBox.IsChecked != false;
        var selectedSubs = CollectSelectedSubsystemNames();
        var baseScope = Analysis.BuildScope(_subsystemTree, selectedSubs, includeDescendants, _extraObjects);
        var sourceNames = baseScope.HasFilter
            ? baseScope.ObjectNames
            : (IReadOnlySet<string>)SubsystemCatalog.Flatten(_subsystemTree)
                .SelectMany(node => node.Content)
                .Concat(_extraObjects)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previously = CollectSelectedObjectNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = ObjectTypeFilter.Build(sourceNames);
        _objectFilterRoots = [];
        _suppressSubsystemSync = true;
        try
        {
            foreach (var group in groups)
            {
                var typeNode = SubsystemTreeNode.Create(group.TypeKey, $"{group.Title} ({group.ObjectNames.Count})");
                foreach (var objectName in group.ObjectNames)
                {
                    var shortName = objectName.Contains('.') ? objectName[(objectName.IndexOf('.') + 1)..] : objectName;
                    var child = SubsystemTreeNode.Create(objectName, shortName);
                    typeNode.AddChild(child);
                }

                _objectFilterRoots.Add(typeNode);
            }

            ObjectFilterTree.ItemsSource = _objectFilterRoots;
            if (previously.Count == 0)
            {
                foreach (var root in _objectFilterRoots)
                    root.IsChecked = true;
            }
            else
            {
                foreach (var node in SubsystemTreeNode.Flatten(_objectFilterRoots))
                {
                    if (previously.Contains(node.FullName))
                        node.IsChecked = true;
                }
            }
        }
        finally
        {
            _suppressSubsystemSync = false;
        }

        AttachObjectFilterHandlers(_objectFilterRoots);

        if (SubsystemStatusText != null)
        {
            SubsystemStatusText.Text = groups.Count == 0
                ? "В выбранных подсистемах нет объектов нужных типов (модули, документы, обработки…)."
                : $"Объектов в фильтре: {groups.Sum(group => group.ObjectNames.Count)}.";
        }
    }

    private void AttachObjectFilterHandlers(IEnumerable<SubsystemTreeNode> roots)
    {
        foreach (var node in SubsystemTreeNode.Flatten(roots))
            node.PropertyChanged += OnObjectFilterNodePropertyChanged;
    }

    private void DetachObjectFilterHandlers(IEnumerable<SubsystemTreeNode> roots)
    {
        foreach (var node in SubsystemTreeNode.Flatten(roots))
            node.PropertyChanged -= OnObjectFilterNodePropertyChanged;
    }

    private List<string> CollectSelectedObjectNames() =>
        SubsystemTreeNode.Flatten(_objectFilterRoots)
            .Where(node => node.IsChecked == true && node.FullName.Contains('.'))
            .Select(node => node.FullName)
            .ToList();

    private TransferScope BuildCurrentScope()
    {
        var includeDescendants = IncludeDescendantsBox.IsChecked != false;
        var selectedSubs = CollectSelectedSubsystemNames();
        var baseScope = Analysis.BuildScope(_subsystemTree, selectedSubs, includeDescendants, _extraObjects);
        var selectedObjects = CollectSelectedObjectNames();
        if (selectedObjects.Count == 0 && _objectFilterRoots.Count > 0)
        {
            // Дерево объектов есть, но всё снято — пустая область.
            return new TransferScope(selectedSubs, new HashSet<string>(StringComparer.OrdinalIgnoreCase), includeDescendants, _extraObjects.ToList());
        }

        if (selectedObjects.Count == 0)
            return baseScope;

        var set = selectedObjects.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var extra in _extraObjects)
            set.Add(extra);
        return new TransferScope(selectedSubs, set, includeDescendants, _extraObjects.ToList());
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
            SyncSessionFromRows();
            _session.TypicalPath = TypicalBox.Text?.Trim();
            _session.CustomizedPath = CustomizedBox.Text?.Trim();
            Analysis.WriteTransferReports(folder, _transfer, writeDraft: true, _session);
            _lastDraftFolder = folder;
            if (TransferCoverageButton != null)
                TransferCoverageButton.IsEnabled = true;
            var coverage = CoverageChecker.Analyze(_transfer, Path.Combine(folder, "extension"), _session);
            SetTransferStatus(
                $"Черновик: {folder}. Контроль — покрыто {coverage.Covered}, не найдено {coverage.Missing}, " +
                $"принято {coverage.Accepted}. Файлы: extension/, coverage.html, transfer-session.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetTransferStatus(exception.Message);
        }
    }

    private async void OnSaveSession(object? sender, RoutedEventArgs e)
    {
        if (_transfer == null && _transfers.Count == 0)
        {
            SetTransferStatus("Сначала найдите доработки.");
            return;
        }

        var path = await PickSaveJsonFile("Куда сохранить transfer-session.json");
        if (path == null)
            return;

        try
        {
            SyncSessionFromRows();
            _session.ExtensionName = TransferNameBox.Text?.Trim() ?? _session.ExtensionName;
            _session.Prefix = TransferPrefixBox.Text?.Trim() ?? _session.Prefix;
            _session.TypicalPath = TypicalBox.Text?.Trim();
            _session.CustomizedPath = CustomizedBox.Text?.Trim();
            _session.LastDraftPath = _lastDraftFolder;
            TransferSessionStore.Save(path, _session);
            SetTransferStatus($"Сессия сохранена: {path}. {DescribeSessionProgress()}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetTransferStatus(exception.Message);
        }
    }

    private async void OnLoadSession(object? sender, RoutedEventArgs e)
    {
        var path = await PickJsonFile("Открыть transfer-session.json");
        if (path == null)
            return;

        try
        {
            var loaded = TransferSessionStore.TryLoad(path);
            if (loaded == null)
            {
                SetTransferStatus("Не удалось прочитать сессию.");
                return;
            }

            _session = loaded;
            if (!string.IsNullOrWhiteSpace(loaded.TypicalPath))
                TypicalBox.Text = loaded.TypicalPath;
            if (!string.IsNullOrWhiteSpace(loaded.CustomizedPath))
                CustomizedBox.Text = loaded.CustomizedPath;
            if (!string.IsNullOrWhiteSpace(loaded.ExtensionName))
                TransferNameBox.Text = loaded.ExtensionName;
            if (!string.IsNullOrWhiteSpace(loaded.Prefix))
                TransferPrefixBox.Text = loaded.Prefix;
            _lastDraftFolder = loaded.LastDraftPath;
            if (TransferCoverageButton != null)
                TransferCoverageButton.IsEnabled = !string.IsNullOrEmpty(_lastDraftFolder);

            foreach (var row in _transfers)
                row.SetWorkStatus(_session.GetStatus(row.ItemKey));

            ApplyTransferFilter();
            SetTransferStatus($"Сессия загружена ({loaded.Items.Count} отметок). {DescribeSessionProgress()}");
            TransferSettingsExpander.IsExpanded = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            SetTransferStatus(exception.Message);
        }
    }

    private async void OnCheckCoverage(object? sender, RoutedEventArgs e)
    {
        if (_transfer == null)
        {
            SetTransferStatus("Сначала найдите доработки.");
            return;
        }

        var folder = _lastDraftFolder;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(Path.Combine(folder, "extension")))
            folder = await PickFolder("Папка с черновиком (содержит extension/)");
        if (folder == null)
            return;

        try
        {
            SyncSessionFromRows();
            var extensionDir = Directory.Exists(Path.Combine(folder, "extension"))
                ? Path.Combine(folder, "extension")
                : folder;
            var reportDir = Directory.Exists(Path.Combine(folder, "extension"))
                ? folder
                : Path.GetDirectoryName(folder) ?? folder;
            var coverage = CoverageChecker.Analyze(_transfer, extensionDir, _session);
            CoverageChecker.WriteReports(reportDir, coverage);
            _lastDraftFolder = reportDir;
            SetTransferStatus(
                $"Контроль: покрыто {coverage.Covered}, не найдено {coverage.Missing}, " +
                $"не требуется {coverage.NotRequired}, принято {coverage.Accepted}. Отчёт: {Path.Combine(reportDir, "coverage.html")}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetTransferStatus(exception.Message);
        }
    }

    private void OnMarkDone(object? sender, RoutedEventArgs e) => MarkSelectedWork(TransferWorkStatus.Done);

    private void OnMarkSkipped(object? sender, RoutedEventArgs e) => MarkSelectedWork(TransferWorkStatus.Skipped);

    private void OnMarkTodo(object? sender, RoutedEventArgs e) => MarkSelectedWork(TransferWorkStatus.Todo);

    private void MarkSelectedWork(TransferWorkStatus status)
    {
        if (TransferGrid.SelectedItem is not TransferRow row)
        {
            SetTransferStatus("Выберите строку в таблице.");
            return;
        }

        row.SetWorkStatus(status);
        _session.SetStatus(row.ItemKey, status);
        ApplyTransferFilter();
        SetTransferStatus($"{row.ObjectName} {row.Method}: {TransferSession.WorkStatusName(status)}. {DescribeSessionProgress()}");
    }

    private void SyncSessionFromRows()
    {
        foreach (var row in _transfers)
            _session.SetStatus(row.ItemKey, row.WorkStatusValue);
    }

    private void SetTransferSaveEnabled(bool enabled)
    {
        if (TransferSaveButton != null)
            TransferSaveButton.IsEnabled = enabled;
        if (TransferSaveButtonCompact != null)
            TransferSaveButtonCompact.IsEnabled = enabled;
    }

    private string DescribeSessionProgress()
    {
        var todo = _transfers.Count(row => row.WorkStatusValue == TransferWorkStatus.Todo);
        var done = _transfers.Count(row => row.WorkStatusValue == TransferWorkStatus.Done);
        var skipped = _transfers.Count(row => row.WorkStatusValue == TransferWorkStatus.Skipped);
        var blocked = _transfers.Count(row => row.WorkStatusValue == TransferWorkStatus.Blocked);
        return $"Прогресс: в работе {todo}, готово {done}, пропуск {skipped}, блок {blocked}.";
    }

    private async void OnSaveScope(object? sender, RoutedEventArgs e)
    {
        var scope = BuildCurrentScope();
        if (!scope.HasFilter)
        {
            SubsystemStatusText.Text = "Отметьте подсистемы/объекты, затем сохраните область.";
            return;
        }

        var path = await PickSaveJsonFile("Куда сохранить subsystems.json");
        if (path == null)
            return;

        try
        {
            Analysis.WriteTransferScope(path, scope);
            SubsystemStatusText.Text = $"Область сохранена: {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SubsystemStatusText.Text = exception.Message;
        }
    }

    private void OnTransferFilterChanged(object? sender, TextChangedEventArgs e) => ApplyTransferFilter();

    private void OnTransferDecisionFilterChanged(object? sender, SelectionChangedEventArgs e) => ApplyTransferFilter();

    private void OnTransferWorkFilterChanged(object? sender, SelectionChangedEventArgs e) => ApplyTransferFilter();

    private void OnTransferSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (TransferGrid.SelectedItem is not TransferRow row || _transfer == null)
        {
            TransferBeforeText.Text = "";
            TransferAfterText.Text = "";
            TransferExtensionText.Text = "";
            TransferDiffPanel.Children.Clear();
            return;
        }

        var before = MethodText(_transfer.Typical, row.Item);
        var after = MethodText(_transfer.Customized, row.Item);
        TransferBeforeText.Text = before;
        TransferAfterText.Text = after;

        var prefix = TransferPrefixBox.Text?.Trim() ?? _transfer.Prefix;
        TransferExtensionText.Text = row.Item.Decision switch
        {
            TransferDecision.Auto => ExtensionDraftWriter.PreviewMethod(prefix, row.Item),
            TransferDecision.FormProgrammatic => FormProgrammaticWriter.PreviewFragment(prefix, row.Item),
            TransferDecision.DataMigration => "(см. migration/ПереносДанных.bsl)",
            _ => "(в черновик расширения автоматически не уходит — смотрите решение в реестре)",
        };

        RenderDiffPanel(before, after, TransferExtensionText.Text);
    }

    private void RenderDiffPanel(string before, string after, string extensionPreview)
    {
        TransferDiffPanel.Children.Clear();
        var marked = TextDiff.MarkSides(before, after);
        var left = BslText.Lines(marked.Left);
        var right = BslText.Lines(marked.Right);
        var count = Math.Max(left.Count, right.Count);

        for (var i = 0; i < count; i++)
        {
            var leftLine = i < left.Count ? left[i] : "";
            var rightLine = i < right.Count ? right[i] : "";
            if (leftLine.StartsWith("- ", StringComparison.Ordinal))
                TransferDiffPanel.Children.Add(CreateHighlightedLine(leftLine, DiffLineTone.Delete));
            else if (rightLine.StartsWith("+ ", StringComparison.Ordinal))
                TransferDiffPanel.Children.Add(CreateHighlightedLine(rightLine, DiffLineTone.Insert));
            else
                TransferDiffPanel.Children.Add(
                    CreateHighlightedLine(rightLine.Length > 0 ? rightLine : leftLine, DiffLineTone.Normal));
        }

        if (!string.IsNullOrWhiteSpace(extensionPreview) && !extensionPreview.StartsWith('('))
        {
            TransferDiffPanel.Children.Add(CreateHighlightedLine("", DiffLineTone.Normal));
            TransferDiffPanel.Children.Add(
                CreateHighlightedLine("// ——— фрагмент для расширения ———", DiffLineTone.Marker));
            foreach (var line in BslText.Lines(extensionPreview))
            {
                var tone = line.StartsWith('#') || line.StartsWith('&')
                    ? DiffLineTone.Marker
                    : DiffLineTone.Normal;
                TransferDiffPanel.Children.Add(CreateHighlightedLine(line, tone));
            }
        }
    }

    private enum DiffLineTone
    {
        Normal,
        Delete,
        Insert,
        Marker,
    }

    private static TextBlock CreateHighlightedLine(string line, DiffLineTone tone)
    {
        var block = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
            FontSize = 13,
        };

        var inlines = block.Inlines ?? [];
        foreach (var token in BslHighlighter.Tokenize(line))
        {
            inlines.Add(new Run(token.Text)
            {
                Foreground = TokenBrush(token.Kind, tone),
            });
        }

        return block;
    }

    private static IBrush TokenBrush(BslHighlighter.TokenKind kind, DiffLineTone tone)
    {
        if (tone == DiffLineTone.Delete)
            return new SolidColorBrush(Color.Parse("#C62828"));
        if (tone == DiffLineTone.Insert)
            return new SolidColorBrush(Color.Parse("#2E7D32"));
        if (tone == DiffLineTone.Marker)
            return new SolidColorBrush(Color.Parse("#1565C0"));

        return kind switch
        {
            BslHighlighter.TokenKind.Keyword => new SolidColorBrush(Color.Parse("#0D47A1")),
            BslHighlighter.TokenKind.Directive => new SolidColorBrush(Color.Parse("#6A1B9A")),
            BslHighlighter.TokenKind.Comment => new SolidColorBrush(Color.Parse("#2E7D32")),
            BslHighlighter.TokenKind.String => new SolidColorBrush(Color.Parse("#AD1457")),
            BslHighlighter.TokenKind.Number => new SolidColorBrush(Color.Parse("#E65100")),
            BslHighlighter.TokenKind.DiffMarker => new SolidColorBrush(Color.Parse("#455A64")),
            _ => new SolidColorBrush(Color.Parse("#212121")),
        };
    }

    private void OnTransferLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not TransferRow row)
            return;
        e.Row.Background = new SolidColorBrush(DecisionColor(row.Item.Decision));
    }

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

        var before = MethodText(_result.Before, row.Change);
        var after = MethodText(_result.After, row.Change);
        var marked = TextDiff.MarkSides(before, after);
        BeforeText.Text = marked.Left;
        AfterText.Text = marked.Right;
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

        IEnumerable<TransferRow> rows = _transfers;
        var decisionFilter = TransferDecisionFilter?.SelectedIndex ?? 0;
        rows = decisionFilter switch
        {
            1 => rows.Where(row => row.Item.Decision == TransferDecision.Auto),
            2 => rows.Where(row => row.Item.Decision == TransferDecision.Highlight),
            3 => rows.Where(row => row.Item.Decision == TransferDecision.Manual),
            4 => rows.Where(row => row.Item.Decision == TransferDecision.DataMigration),
            5 => rows.Where(row => row.Item.Decision == TransferDecision.FormProgrammatic),
            6 => rows.Where(row => row.Item.Decision == TransferDecision.Skip),
            _ => rows,
        };

        var workFilter = TransferWorkFilter?.SelectedIndex ?? 0;
        rows = workFilter switch
        {
            1 => rows.Where(row => row.WorkStatusValue == TransferWorkStatus.Todo),
            2 => rows.Where(row => row.WorkStatusValue == TransferWorkStatus.Done),
            3 => rows.Where(row => row.WorkStatusValue == TransferWorkStatus.Skipped),
            4 => rows.Where(row => row.WorkStatusValue == TransferWorkStatus.Blocked),
            _ => rows,
        };

        var filter = TransferFilterBox?.Text?.Trim();
        TransferGrid.ItemsSource = FilterRows(
            rows,
            filter,
            row => $"{row.WorkStatus} {row.ObjectName} {row.Method} {row.Kind} {row.Decision} {row.Hook} {row.Reason}").ToList();
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

    private async Task<string?> PickJsonFile(string title)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                FilePickerFileTypes.All,
            ],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async Task<string?> PickSaveJsonFile(string title)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return null;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = "subsystems.json",
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] },
            ],
        });
        return file?.TryGetLocalPath();
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private void SetHistoryStatus(string text) => HistoryStatusText.Text = text;

    private void SetTransferStatus(string text) => TransferStatusText.Text = text;

    private static Color DecisionColor(TransferDecision decision) => decision switch
    {
        TransferDecision.Auto => Color.Parse("#C8E6C9"),
        TransferDecision.Manual => Color.Parse("#FFE0B2"),
        TransferDecision.Highlight => Color.Parse("#BBDEFB"),
        TransferDecision.Skip => Color.Parse("#EEEEEE"),
        TransferDecision.DataMigration => Color.Parse("#E1BEE7"),
        TransferDecision.FormProgrammatic => Color.Parse("#B2EBF2"),
        _ => Colors.Transparent,
    };

    private static string DescribeTransfer(TransferPlan plan)
    {
        var auto = plan.Items.Count(item => item.Decision == TransferDecision.Auto);
        var manual = plan.Items.Count(item => item.Decision == TransferDecision.Manual);
        var highlight = plan.Items.Count(item => item.Decision == TransferDecision.Highlight);
        var data = plan.Items.Count(item => item.Decision == TransferDecision.DataMigration);
        var forms = plan.Items.Count(item => item.Decision == TransferDecision.FormProgrammatic);
        var skip = plan.Items.Count(item => item.Decision == TransferDecision.Skip);
        var manualObjects = plan.Items
            .Where(item => item.Decision == TransferDecision.Manual)
            .Select(item => item.ObjectName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var scope = !plan.Scope.HasFilter
            ? "вся конфигурация"
            : $"{plan.Scope.SelectedSubsystemNames.Count} подсистем + {plan.Scope.ExtraObjectNames.Count} доп. / {plan.Scope.ObjectNames.Count} объектов";
        var manualPart = manual == 0
            ? "вручную 0"
            : $"вручную {manual} ({manualObjects} объ.)";
        return $"Доработок: {plan.Items.Count} ({scope}). Авто {auto}, {manualPart}, подсветка {highlight}, данные {data}, формы {forms}, пропуск {skip}.";
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

    private static string MethodText(ConfigurationDump dump, TransferItem item)
    {
        if (item.Method == null || item.Module == null)
            return item.Reason;

        if (!string.IsNullOrEmpty(item.AddedMethodSource))
            return item.AddedMethodSource;

        var owner = dump.Objects.FirstOrDefault(entry =>
            (!string.IsNullOrEmpty(item.ObjectUuid) && entry.Uuid.Equals(item.ObjectUuid, StringComparison.OrdinalIgnoreCase))
            || entry.FullName.Equals(item.ObjectName, StringComparison.OrdinalIgnoreCase));
        var module = owner?.Modules.FirstOrDefault(entry =>
            entry.RelativePath.Equals(item.Module, StringComparison.OrdinalIgnoreCase));
        var method = module?.Methods.FirstOrDefault(entry =>
            entry.Name.Equals(item.Method, StringComparison.OrdinalIgnoreCase));
        if (method == null || string.IsNullOrWhiteSpace(method.BodyText))
            return item.Reason;

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

public sealed class TransferRow(TransferItem item) : INotifyPropertyChanged
{
    private TransferWorkStatus _workStatus = TransferWorkStatus.Todo;

    public TransferItem Item => item;
    public string ItemKey => TransferSession.ItemKey(item);
    public string ObjectName => item.ObjectName;
    public string Method => item.Method ?? "";
    public string Kind => ReportWriter.KindName(item.ChangeKind);
    public string Decision => ReportWriter.TransferDecisionName(item.Decision);
    public string Hook => ReportWriter.ProposedHookName(item.ProposedHook);
    public string Reason => item.Reason;
    public string WorkStatus => TransferSession.WorkStatusName(_workStatus);
    public TransferWorkStatus WorkStatusValue => _workStatus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetWorkStatus(TransferWorkStatus status, bool notify = true)
    {
        if (_workStatus == status)
            return;
        _workStatus = status;
        if (notify)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkStatus)));
    }
}

/// <summary>
/// Узел дерева с каскадными галочками (подсистемы или объекты области).
/// </summary>
public sealed class SubsystemTreeNode : INotifyPropertyChanged
{
    private bool? _isChecked;
    private bool _suppress;

    private SubsystemTreeNode(string fullName, string title)
    {
        FullName = fullName;
        Title = title;
        _isChecked = false;
        Children = [];
    }

    public static SubsystemTreeNode Create(string fullName, string title) => new(fullName, title);

    public void AddChild(SubsystemTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public string FullName { get; }
    public string Title { get; }
    public ObservableCollection<SubsystemTreeNode> Children { get; }
    public SubsystemTreeNode? Parent { get; private set; }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value, propagateDown: true, propagateUp: true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static SubsystemTreeNode FromInfo(SubsystemInfo info)
    {
        var node = new SubsystemTreeNode(info.FullName, $"{info.Name} ({info.Content.Count})");
        foreach (var childInfo in info.Children)
        {
            var child = FromInfo(childInfo);
            child.Parent = node;
            node.Children.Add(child);
        }

        return node;
    }

    public static IEnumerable<SubsystemTreeNode> Flatten(IEnumerable<SubsystemTreeNode> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Flatten(root.Children))
                yield return child;
        }
    }

    public void SetCheckedSilent(bool? value) => SetChecked(value, propagateDown: false, propagateUp: false);

    private void SetChecked(bool? value, bool propagateDown, bool propagateUp)
    {
        if (_isChecked == value)
            return;
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));
        if (_suppress)
            return;

        if (propagateDown && value.HasValue)
        {
            _suppress = true;
            foreach (var child in Children)
                child.SetChecked(value, propagateDown: true, propagateUp: false);
            _suppress = false;
        }

        if (propagateUp)
            Parent?.UpdateFromChildren();
    }

    private void UpdateFromChildren()
    {
        if (Children.Count == 0 || _suppress)
            return;

        bool? next;
        if (Children.All(child => child.IsChecked == true))
            next = true;
        else if (Children.All(child => child.IsChecked == false))
            next = false;
        else
            next = null;

        if (_isChecked == next)
        {
            Parent?.UpdateFromChildren();
            return;
        }

        _suppress = true;
        _isChecked = next;
        OnPropertyChanged(nameof(IsChecked));
        _suppress = false;
        Parent?.UpdateFromChildren();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
