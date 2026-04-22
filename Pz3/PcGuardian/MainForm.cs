namespace PcGuardian;

public sealed class MainForm : Form
{
    private readonly Button _quickScanButton = new();
    private readonly Button _deepScanButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _exportButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _healthLabel = new();
    private readonly Label _highTile = new();
    private readonly Label _mediumTile = new();
    private readonly Label _lowTile = new();
    private readonly Label _filesTile = new();
    private readonly ProgressBar _progressBar = new();
    private readonly DataGridView _findingsGrid = new();
    private readonly ListBox _logList = new();

    private CancellationTokenSource? _scanCancellation;
    private ScanSummary? _lastSummary;

    public MainForm()
    {
        Text = "PC Guardian";
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(980, 660);
        BackColor = Color.FromArgb(238, 242, 247);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Shield;
        BuildInterface();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 67));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateActions(), 0, 1);
        root.Controls.Add(CreateFindingsArea(), 0, 2);
        root.Controls.Add(CreateLogArea(), 0, 3);
        root.Controls.Add(CreateFooter(), 0, 4);
        ResetMetricTiles();
    }

    private Control CreateHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 29, 48),
            Padding = new Padding(24, 18, 24, 18),
            Margin = new Padding(0, 0, 0, 14)
        };

        var title = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 24F),
            Text = "PC Guardian",
            Location = new Point(20, 18)
        };
        header.Controls.Add(title);

        var subtitle = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(197, 207, 224),
            Font = new Font("Segoe UI", 10.5F),
            Text = "Desktop-анализатор безопасности компьютера на C#",
            Location = new Point(23, 72)
        };
        header.Controls.Add(subtitle);

        var healthPanel = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(330, 66),
            Location = new Point(ClientSize.Width - 390, 27),
            BackColor = Color.FromArgb(32, 45, 72)
        };
        _healthLabel.Dock = DockStyle.Fill;
        _healthLabel.ForeColor = Color.White;
        _healthLabel.Font = new Font("Segoe UI Semibold", 14F);
        _healthLabel.TextAlign = ContentAlignment.MiddleCenter;
        _healthLabel.Text = "Система не проверялась";
        healthPanel.Controls.Add(_healthLabel);
        header.Controls.Add(healthPanel);
        return header;
    }

    private Control CreateActions()
    {
        var area = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = BackColor, Margin = new Padding(0, 0, 0, 12) };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 690));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), WrapContents = false };
        ConfigureButton(_quickScanButton, "Быстрый анализ", Color.FromArgb(28, 112, 190));
        ConfigureButton(_deepScanButton, "Глубокий анализ", Color.FromArgb(33, 132, 95));
        ConfigureButton(_cancelButton, "Остановить", Color.FromArgb(190, 72, 63));
        ConfigureButton(_exportButton, "Сохранить отчет", Color.FromArgb(75, 88, 108));
        _cancelButton.Enabled = false;
        _exportButton.Enabled = false;
        _quickScanButton.Click += async (_, _) => await StartScanAsync(false);
        _deepScanButton.Click += async (_, _) => await StartScanAsync(true);
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();
        _exportButton.Click += (_, _) => ExportReport();
        actions.Controls.AddRange([_quickScanButton, _deepScanButton, _cancelButton, _exportButton]);
        area.Controls.Add(actions, 0, 0);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, BackColor = BackColor, Margin = new Padding(12, 0, 0, 0) };
        for (var i = 0; i < 4; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        metrics.Controls.Add(CreateMetricCard("Высокий", _highTile, Color.FromArgb(207, 55, 55)), 0, 0);
        metrics.Controls.Add(CreateMetricCard("Средний", _mediumTile, Color.FromArgb(196, 135, 33)), 1, 0);
        metrics.Controls.Add(CreateMetricCard("Низкий", _lowTile, Color.FromArgb(42, 126, 196)), 2, 0);
        metrics.Controls.Add(CreateMetricCard("Файлов", _filesTile, Color.FromArgb(81, 92, 110)), 3, 0);
        area.Controls.Add(metrics, 1, 0);
        return area;
    }

    private Control CreateFindingsArea()
    {
        var panel = CreateSectionPanel("Найденные риски");
        _findingsGrid.Dock = DockStyle.Fill;
        _findingsGrid.BackgroundColor = Color.White;
        _findingsGrid.BorderStyle = BorderStyle.None;
        _findingsGrid.AllowUserToAddRows = false;
        _findingsGrid.ReadOnly = true;
        _findingsGrid.RowHeadersVisible = false;
        _findingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _findingsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _findingsGrid.EnableHeadersVisualStyles = false;
        _findingsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 247, 251);
        _findingsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        _findingsGrid.Columns.Add("Risk", "Риск");
        _findingsGrid.Columns.Add("Category", "Категория");
        _findingsGrid.Columns.Add("Title", "Находка");
        _findingsGrid.Columns.Add("Location", "Расположение");
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_findingsGrid, 0, 1);
        return panel;
    }

    private Control CreateLogArea()
    {
        var panel = CreateSectionPanel("Журнал анализа");
        _logList.Dock = DockStyle.Fill;
        _logList.BorderStyle = BorderStyle.None;
        _logList.BackColor = Color.FromArgb(249, 251, 253);
        _logList.Font = new Font("Consolas", 9.5F);
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_logList, 0, 1);
        return panel;
    }

    private Panel CreateSectionPanel(string caption)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = caption,
            Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = Color.FromArgb(31, 42, 58),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        return panel;
    }

    private Control CreateFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = BackColor };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI Semibold", 10F);
        _statusLabel.Text = "Готов к анализу";
        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.Text = "Риски: 0";
        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Margin = new Padding(0, 11, 0, 11);
        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_summaryLabel, 1, 0);
        footer.Controls.Add(_progressBar, 2, 0);
        return footer;
    }

    private static Control CreateMetricCard(string title, Label valueLabel, Color accent)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 8, 0) };
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
        card.Controls.Add(new Label { AutoSize = true, Text = title, Location = new Point(18, 10), ForeColor = Color.FromArgb(93, 107, 128) });
        valueLabel.AutoSize = true;
        valueLabel.Text = "0";
        valueLabel.Location = new Point(17, 30);
        valueLabel.Font = new Font("Segoe UI Semibold", 20F);
        card.Controls.Add(valueLabel);
        return card;
    }

    private static void ConfigureButton(Button button, string text, Color color)
    {
        button.Text = text;
        button.Width = 156;
        button.Height = 44;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Margin = new Padding(0, 0, 10, 0);
        button.UseVisualStyleBackColor = false;
    }

    private async Task StartScanAsync(bool deepScan)
    {
        SetScanState(true);
        _findingsGrid.Rows.Clear();
        _logList.Items.Clear();
        _progressBar.Value = 0;
        ResetMetricTiles();
        _healthLabel.Text = "Анализ выполняется";
        _statusLabel.Text = deepScan ? "Идет глубокий анализ..." : "Идет быстрый анализ...";
        AddLog("Приложение запущено с правами администратора.");
        _scanCancellation = new CancellationTokenSource();
        var scanner = new SecurityScanner(new Progress<string>(AddLog), new Progress<int>(v => _progressBar.Value = Math.Clamp(v, 0, 100)));

        try
        {
            _lastSummary = deepScan
                ? await scanner.RunDeepScanAsync(_scanCancellation.Token)
                : await scanner.RunQuickScanAsync(_scanCancellation.Token);
            ShowSummary(_lastSummary);
            AddLog("Готово. Отчет можно сохранить в файл.");
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Сканирование остановлено";
            AddLog("Сканирование остановлено пользователем.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка анализа";
            AddLog($"Ошибка: {ex.Message}");
            MessageBox.Show(ex.Message, "Ошибка анализа", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            SetScanState(false);
        }
    }

    private void ShowSummary(ScanSummary summary)
    {
        foreach (var finding in summary.Findings)
        {
            var rowIndex = _findingsGrid.Rows.Add(finding.Risk, finding.Category, finding.Title, finding.Location);
            _findingsGrid.Rows[rowIndex].DefaultCellStyle.BackColor = finding.Risk switch
            {
                RiskLevel.High => Color.FromArgb(255, 235, 235),
                RiskLevel.Medium => Color.FromArgb(255, 248, 226),
                RiskLevel.Low => Color.FromArgb(234, 245, 255),
                _ => Color.White
            };
        }

        _highTile.Text = summary.HighCount.ToString();
        _mediumTile.Text = summary.MediumCount.ToString();
        _lowTile.Text = summary.LowCount.ToString();
        _filesTile.Text = summary.FilesChecked.ToString();
        _healthLabel.Text = summary.HighCount > 0 ? "Требуется проверка" : summary.MediumCount > 0 ? "Есть замечания" : "Серьезных рисков нет";
        _statusLabel.Text = $"Завершено за {(summary.FinishedAt - summary.StartedAt).TotalSeconds:0} сек.";
        _summaryLabel.Text = $"Высокий: {summary.HighCount}   Средний: {summary.MediumCount}   Низкий: {summary.LowCount}   Файлов: {summary.FilesChecked}";
        _exportButton.Enabled = true;
    }

    private void ResetMetricTiles()
    {
        _highTile.Text = "0";
        _mediumTile.Text = "0";
        _lowTile.Text = "0";
        _filesTile.Text = "0";
    }

    private void SetScanState(bool isScanning)
    {
        _quickScanButton.Enabled = !isScanning;
        _deepScanButton.Enabled = !isScanning;
        _cancelButton.Enabled = isScanning;
        _exportButton.Enabled = !isScanning && _lastSummary is not null;
    }

    private void AddLog(string message)
    {
        _logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        _logList.TopIndex = Math.Max(0, _logList.Items.Count - 1);
    }

    private void ExportReport()
    {
        if (_lastSummary is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить отчет",
            Filter = "Текстовый отчет (*.txt)|*.txt",
            FileName = $"pc-guardian-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, SecurityScanner.BuildReport(_lastSummary));
            AddLog($"Отчет сохранен: {dialog.FileName}");
        }
    }
}
