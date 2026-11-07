namespace TypovayaUp.Launcher;

public sealed class MainForm : Form
{
    readonly TextBox _txtSource = new() { Width = 420 };
    readonly TextBox _txtVendor = new() { Width = 420 };
    readonly TextBox _txtSettings = new() { Width = 420 };
    readonly TextBox _txtOut = new() { Width = 420 };
    readonly TextBox _txtExtension = new() { Width = 200, Text = "УП_Доработки" };
    readonly TextBox _txtPrefix = new() { Width = 80, Text = "УП_" };
    readonly TextBox _txtOscript = new() { Width = 420 };
    readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 9f), WordWrap = false };
    readonly Label _status = new() { AutoSize = true };
    readonly Button _btnRun = new() { Text = "Сравнить", AutoSize = true };
    readonly Button _btnCancel = new() { Text = "Отмена", AutoSize = true, Enabled = false };
    readonly Button _btnReport = new() { Text = "Открыть отчёт", AutoSize = true, Enabled = false };
    readonly Button _btnFolder = new() { Text = "Открыть папку результата", AutoSize = true, Enabled = false };
    readonly Button _btnDiag = new() { Text = "Диагностика", AutoSize = true };
    readonly CheckBox _chkForms = new() { Text = "Анализ форм (forms_analysis.json)", AutoSize = true };
    readonly CheckBox _chkFormsPreview = new() { Text = "Превью реквизитов (forms_preview/, не extension)", AutoSize = true };
    readonly TextBox _txtDistro = new() { Width = 420 };
    readonly Button _btnScanDistro = new() { Text = "Проверить дистрибутив", AutoSize = true };
    readonly Button _btnInstallPlatform = new() { Text = "Установить CSServer (UAC)", AutoSize = true };
    readonly Button _btnCheckIbcmd = new() { Text = "Проверить ibcmd", AutoSize = true };

    readonly string _typovayaRoot;
    OscriptRunSession? _session;
    CancellationTokenSource? _runCts;
    string? _lastOutDir;

    public MainForm()
    {
        Text = "typovaya-up — сравнение выгрузок";
        Width = 920;
        Height = 700;
        _typovayaRoot = LauncherPaths.ResolveTypovayaUpRoot();

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(8),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddRow(top, "Исходная (УП):", _txtSource, BrowseFolder(_txtSource));
        AddRow(top, "Поставщик (БП):", _txtVendor, BrowseFolder(_txtVendor));
        AddRow(top, "Настройки merge (необяз.)", _txtSettings, BrowseFile(_txtSettings, "XML|*.xml"));
        AddRow(top, "Папка результата:", _txtOut, BrowseFolder(_txtOut));
        AddRow(top, "Имя расширения:", _txtExtension, null);
        AddRow(top, "Префикс:", _txtPrefix, null);
        AddRow(top, "oscript.exe (пусто = PATH/OSCRIPT):", _txtOscript, BrowseFile(_txtOscript, "oscript|oscript.exe|*.exe"));
        top.Controls.Add(_chkForms, 1, top.RowCount);
        top.SetColumnSpan(_chkForms, 2);
        top.RowCount++;
        top.Controls.Add(_chkFormsPreview, 1, top.RowCount);
        top.SetColumnSpan(_chkFormsPreview, 2);
        top.RowCount++;

        var plat = new GroupBox { Text = "Платформа 1С (опционально, не нужна для сравнения)", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var platGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        platGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        platGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        platGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddRow(platGrid, "Папка дистрибутива:", _txtDistro, BrowseFolder(_txtDistro));
        var platBtns = new FlowLayoutPanel { AutoSize = true };
        platBtns.Controls.AddRange([_btnScanDistro, _btnInstallPlatform, _btnCheckIbcmd]);
        platGrid.Controls.Add(platBtns, 1, 1);
        platGrid.SetColumnSpan(platBtns, 2);
        plat.Controls.Add(platGrid);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        buttons.Controls.AddRange([_btnRun, _btnCancel, _btnDiag, _btnReport, _btnFolder, _status]);

        _btnRun.Click += async (_, _) => await RunCompareAsync().ConfigureAwait(true);
        _btnCancel.Click += async (_, _) => await CancelRunAsync().ConfigureAwait(true);
        _btnDiag.Click += async (_, _) => await RunDiagnosticsAsync().ConfigureAwait(true);
        _btnReport.Click += (_, _) => OpenReport();
        _btnFolder.Click += (_, _) => OpenOutputFolder();
        _btnScanDistro.Click += (_, _) => ScanDistribution();
        _btnInstallPlatform.Click += (_, _) => InstallCsserver();
        _btnCheckIbcmd.Click += (_, _) => CheckIbcmd();

        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        logPanel.Controls.Add(_log);

        Controls.Add(logPanel);
        Controls.Add(buttons);
        Controls.Add(plat);
        Controls.Add(top);

        AppendLog($"Корень typovaya-up: {_typovayaRoot}");
        _txtOut.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "typovaya-up-out");
        _txtDistro.Text = Path.Combine(_typovayaRoot, "distributions", "1c");
    }

    void ScanDistribution()
    {
        var scan = PlatformDistributionScanner.Scan(_txtDistro.Text.Trim());
        AppendLog("—— Дистрибутив 1С ——");
        foreach (var n in scan.Notes) AppendLog(n);
        foreach (var m in scan.MsiFiles) AppendLog("MSI: " + m);
        foreach (var t in scan.MstFiles) AppendLog("MST: " + t);
        _btnInstallPlatform.Enabled = PlatformDistributionScanner.IsInstallable(scan);
    }

    void InstallCsserver()
    {
        var scan = PlatformDistributionScanner.Scan(_txtDistro.Text.Trim());
        if (!PlatformDistributionScanner.IsInstallable(scan))
        {
            MessageBox.Show(this, "Сначала положите MSI в папку (см. distributions/1c/README.md).", "Установка", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var text = "Будет запущен msiexec с правами администратора (CSServer, без службы).\nПродолжить?";
        if (MessageBox.Show(this, text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        try
        {
            var plan = PlatformCsserverInstall.BuildPlan(scan, Path.Combine(_typovayaRoot, "distributions", "1c", "_logs"));
            PlatformInstallRunner.StartElevated(plan);
            AppendLog("Установка запущена (UAC). Лог: " + plan.LogFilePath);
            AppendLog("После завершения нажмите «Проверить ibcmd».");
        }
        catch (Exception ex) { AppendLog("Ошибка: " + ex.Message); }
    }

    void CheckIbcmd()
    {
        var found = PlatformCsserverInstall.FindIbcmdCandidates();
        if (found.Count == 0)
            AppendLog("ibcmd не найден.");
        else
            foreach (var p in found) AppendLog("ibcmd: " + p);
    }

    static void AddRow(TableLayoutPanel table, string label, Control editor, Control? extra)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(editor, 1, row);
        if (extra is not null)
            table.Controls.Add(extra, 2, row);
    }

    static Button BrowseFolder(TextBox target)
    {
        var btn = new Button { Text = "…", AutoSize = true };
        btn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.SelectedPath;
        };
        return btn;
    }

    static Button BrowseFile(TextBox target, string filter)
    {
        var btn = new Button { Text = "…", AutoSize = true };
        btn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == DialogResult.OK)
                target.Text = dlg.FileName;
        };
        return btn;
    }

    async Task RunDiagnosticsAsync()
    {
        _btnDiag.Enabled = false;
        try
        {
            AppendLog("—— Диагностика зависимостей ——");
            var oscript = string.IsNullOrWhiteSpace(_txtOscript.Text) ? null : _txtOscript.Text.Trim();
            var results = await DependencyDiagnostics.RunAsync(oscript, _typovayaRoot).ConfigureAwait(true);
            foreach (var r in results)
            {
                var mark = r.Ok ? "OK" : (r.Kind == DependencyKind.Required ? "ОШИБКА" : "—");
                AppendLog($"[{mark}] {r.Title}: {r.Detail}");
            }
        }
        finally
        {
            _btnDiag.Enabled = true;
        }
    }

    async Task RunCompareAsync()
    {
        var source = _txtSource.Text.Trim();
        var vendor = _txtVendor.Text.Trim();
        var settings = _txtSettings.Text.Trim();
        var output = _txtOut.Text.Trim();

        foreach (var err in new[]
                 {
                     PathSafety.ValidateDumpFolder(source, "исходная"),
                     PathSafety.ValidateDumpFolder(vendor, "поставщик"),
                     PathSafety.ValidateOptionalSettingsFile(string.IsNullOrEmpty(settings) ? null : settings),
                     PathSafety.ValidateOutputVersusSources(output, source, vendor),
                 })
        {
            if (err is not null)
            {
                MessageBox.Show(this, err, "Проверка входов", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var oscriptPath = DependencyDiagnostics.ResolveOscriptPath(
            string.IsNullOrWhiteSpace(_txtOscript.Text) ? null : _txtOscript.Text.Trim());
        if (oscriptPath is null)
        {
            MessageBox.Show(this,
                "OneScript не найден. Запустите диагностику или укажите путь к oscript.exe.",
                "Зависимости", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var mainScript = Path.Combine(_typovayaRoot, "src", "Главный.os");
        var request = new CompareLaunchRequest
        {
            OscriptExecutable = oscriptPath,
            MainScriptPath = mainScript,
            SourceDumpPath = source,
            VendorDumpPath = vendor,
            SettingsXmlPath = string.IsNullOrEmpty(settings) ? null : settings,
            OutputDirectory = output,
            ExtensionName = _txtExtension.Text.Trim(),
            NamePrefix = _txtPrefix.Text.Trim(),
            AnalyzeForms = _chkForms.Checked || _chkFormsPreview.Checked,
            FormsPreview = _chkFormsPreview.Checked,
            WorkingDirectory = _typovayaRoot,
        };

        _log.Clear();
        _btnRun.Enabled = false;
        _btnCancel.Enabled = true;
        _btnReport.Enabled = false;
        _btnFolder.Enabled = false;
        _status.Text = "Выполняется…";
        _lastOutDir = output;
        _runCts = new CancellationTokenSource();
        _session = new OscriptRunSession();
        _session.OnStdout += AppendLog;
        _session.OnStderr += line => AppendLog("[stderr] " + line);
        _session.OnCompleted += OnRunCompleted;

        try
        {
            await _session.StartAsync(request, _runCts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendLog("Сбой запуска: " + ex.Message);
            _status.Text = "Ошибка запуска";
        }
        finally
        {
            _btnRun.Enabled = true;
            _btnCancel.Enabled = false;
        }
    }

    void OnRunCompleted(RunCompletionState state, int? exitCode)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnRunCompleted(state, exitCode));
            return;
        }

        switch (state)
        {
            case RunCompletionState.Succeeded:
                _status.Text = "Готово, код 0";
                _btnReport.Enabled = true;
                _btnFolder.Enabled = true;
                break;
            case RunCompletionState.Failed:
                _status.Text = $"Завершено с ошибкой, код {exitCode}";
                _btnFolder.Enabled = Directory.Exists(_lastOutDir);
                AppendLog("Результат может быть неполным — проверьте log.txt в папке выхода.");
                break;
            case RunCompletionState.Cancelled:
                _status.Text = "Отменено пользователем";
                _btnFolder.Enabled = Directory.Exists(_lastOutDir);
                AppendLog("Запуск прерван. Результат может быть неполным.");
                break;
        }
    }

    async Task CancelRunAsync()
    {
        if (_session is not null)
            await _session.CancelAsync().ConfigureAwait(true);
        _runCts?.Cancel();
    }

    void AppendLog(string line)
    {
        if (_log.InvokeRequired)
        {
            _log.BeginInvoke(() => AppendLog(line));
            return;
        }
        _log.AppendText(line + Environment.NewLine);
    }

    void OpenReport()
    {
        if (_lastOutDir is null) return;
        var html = Path.Combine(_lastOutDir, "razbor_moduley.html");
        if (!File.Exists(html))
        {
            MessageBox.Show(this, "Файл отчёта не найден: " + html);
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(html) { UseShellExecute = true });
    }

    void OpenOutputFolder()
    {
        if (_lastOutDir is null || !Directory.Exists(_lastOutDir))
            return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_lastOutDir) { UseShellExecute = true });
    }
}
