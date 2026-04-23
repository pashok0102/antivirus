using System.ComponentModel;

namespace PcGuardian;

public sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(242, 246, 250);
    private static readonly Color Surface = Color.White;
    private static readonly Color SurfaceSoft = Color.FromArgb(248, 251, 253);
    private static readonly Color Ink = Color.FromArgb(22, 33, 51);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);
    private static readonly Color Navy = Color.FromArgb(15, 27, 46);
    private static readonly Color NavySoft = Color.FromArgb(27, 44, 72);
    private static readonly Color Cyan = Color.FromArgb(18, 176, 180);
    private static readonly Color Blue = Color.FromArgb(40, 121, 216);
    private static readonly Color Green = Color.FromArgb(21, 145, 105);
    private static readonly Color Red = Color.FromArgb(204, 72, 72);
    private static readonly Color Amber = Color.FromArgb(207, 143, 36);

    private readonly ActionButton _quickScanButton = new("Быстрый анализ", Blue);
    private readonly ActionButton _deepScanButton = new("Глубокий анализ", Green);
    private readonly ActionButton _cancelButton = new("Остановить", Red);
    private readonly ActionButton _exportButton = new("Сохранить отчет", Color.FromArgb(73, 86, 107));

    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _healthLabel = new();
    private readonly Label _healthCaptionLabel = new();
    private readonly MetricCard _highCard = new("Высокий", Red);
    private readonly MetricCard _mediumCard = new("Средний", Amber);
    private readonly MetricCard _lowCard = new("Низкий", Blue);
    private readonly MetricCard _filesCard = new("Файлов", Color.FromArgb(94, 107, 128));
    private readonly ProgressBar _progressBar = new();
    private readonly DataGridView _findingsGrid = new();
    private readonly ListBox _logList = new();

    private CancellationTokenSource? _scanCancellation;
    private ScanSummary? _lastSummary;

    public MainForm()
    {
        Text = "PC Guardian";
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(1020, 680);
        BackColor = Background;
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(20);
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
            BackColor = Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateDashboard(), 0, 1);
        root.Controls.Add(CreateFindingsArea(), 0, 2);
        root.Controls.Add(CreateLogArea(), 0, 3);
        root.Controls.Add(CreateFooter(), 0, 4);

        ResetMetricTiles();
        UpdateHealth("Система не проверялась", "Запустите быстрый или глубокий анализ");
    }

    private Control CreateHeader()
    {
        var header = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Navy,
            Radius = 8,
            Padding = new Padding(28, 22, 28, 22),
            Margin = new Padding(0, 0, 0, 14)
        };

        var shield = new PictureBox
        {
            Image = Icon?.ToBitmap() ?? SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(56, 56),
            Location = new Point(28, 28)
        };
        header.Controls.Add(shield);

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 25F),
            Text = "PC Guardian",
            Location = new Point(100, 25)
        });

        header.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10.5F),
            Text = "Desktop-анализатор безопасности компьютера",
            Location = new Point(104, 78)
        });

        var healthPanel = new RoundedPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(350, 76),
            Location = new Point(ClientSize.Width - 428, 30),
            BackColor = NavySoft,
            Radius = 8,
            Padding = new Padding(18, 10, 18, 10)
        };

        _healthLabel.Dock = DockStyle.Top;
        _healthLabel.Height = 32;
        _healthLabel.ForeColor = Color.White;
        _healthLabel.Font = new Font("Segoe UI Semibold", 14.5F);
        _healthLabel.TextAlign = ContentAlignment.MiddleLeft;

        _healthCaptionLabel.Dock = DockStyle.Fill;
        _healthCaptionLabel.ForeColor = Color.FromArgb(190, 203, 224);
        _healthCaptionLabel.Font = new Font("Segoe UI", 9.5F);
        _healthCaptionLabel.TextAlign = ContentAlignment.TopLeft;

        healthPanel.Controls.Add(_healthCaptionLabel);
        healthPanel.Controls.Add(_healthLabel);
        header.Controls.Add(healthPanel);

        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Cyan });
        return header;
    }

    private Control CreateDashboard()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Background,
            Margin = new Padding(0, 0, 0, 12)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 650));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var actionsCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Radius = 8,
            Padding = new Padding(18)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            WrapContents = false
        };

        _cancelButton.Enabled = false;
        _exportButton.Enabled = false;
        _quickScanButton.Click += async (_, _) => await StartScanAsync(false);
        _deepScanButton.Click += async (_, _) => await StartScanAsync(true);
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();
        _exportButton.Click += (_, _) => ExportReport();
        actions.Controls.AddRange([_quickScanButton, _deepScanButton, _cancelButton, _exportButton]);
        actionsCard.Controls.Add(actions);
        area.Controls.Add(actionsCard, 0, 0);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            BackColor = Background,
            Margin = new Padding(12, 0, 0, 0)
        };
        for (var i = 0; i < 4; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        metrics.Controls.Add(_highCard, 0, 0);
        metrics.Controls.Add(_mediumCard, 1, 0);
        metrics.Controls.Add(_lowCard, 2, 0);
        metrics.Controls.Add(_filesCard, 3, 0);
        area.Controls.Add(metrics, 1, 0);
        return area;
    }

    private Control CreateFindingsArea()
    {
        var panel = CreateSectionPanel("Найденные риски", "Список элементов, которые требуют внимания или ручной проверки");
        _findingsGrid.Dock = DockStyle.Fill;
        _findingsGrid.BackgroundColor = Surface;
        _findingsGrid.BorderStyle = BorderStyle.None;
        _findingsGrid.AllowUserToAddRows = false;
        _findingsGrid.ReadOnly = true;
        _findingsGrid.RowHeadersVisible = false;
        _findingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _findingsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _findingsGrid.EnableHeadersVisualStyles = false;
        _findingsGrid.GridColor = Color.FromArgb(226, 232, 240);
        _findingsGrid.RowTemplate.Height = 34;
        _findingsGrid.ColumnHeadersHeight = 38;
        _findingsGrid.DefaultCellStyle.BackColor = Surface;
        _findingsGrid.DefaultCellStyle.ForeColor = Ink;
        _findingsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 245, 246);
        _findingsGrid.DefaultCellStyle.SelectionForeColor = Ink;
        _findingsGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
        _findingsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 242, 248);
        _findingsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        _findingsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        _findingsGrid.Columns.Add("Risk", "Риск");
        _findingsGrid.Columns.Add("Category", "Категория");
        _findingsGrid.Columns.Add("Title", "Находка");
        _findingsGrid.Columns.Add("Location", "Расположение");
        _findingsGrid.Columns["Risk"]!.FillWeight = 18;
        _findingsGrid.Columns["Category"]!.FillWeight = 24;
        _findingsGrid.Columns["Title"]!.FillWeight = 44;
        _findingsGrid.Columns["Location"]!.FillWeight = 74;
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_findingsGrid, 0, 1);
        return panel;
    }

    private Control CreateLogArea()
    {
        var panel = CreateSectionPanel("Журнал анализа", "Ход проверки и важные события");
        _logList.Dock = DockStyle.Fill;
        _logList.BorderStyle = BorderStyle.None;
        _logList.BackColor = SurfaceSoft;
        _logList.ForeColor = Color.FromArgb(51, 65, 85);
        _logList.Font = new Font("Consolas", 9.5F);
        _logList.ItemHeight = 18;
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_logList, 0, 1);
        return panel;
    }

    private static RoundedPanel CreateSectionPanel(string title, string subtitle)
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Radius = 8,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 12)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Surface
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font("Segoe UI Semibold", 12.5F),
            ForeColor = Ink,
            Location = new Point(0, 2)
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = subtitle,
            Font = new Font("Segoe UI", 9.2F),
            ForeColor = Muted,
            Location = new Point(1, 30)
        });
        layout.Controls.Add(header, 0, 0);
        return panel;
    }

    private Control CreateFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Background,
            Padding = new Padding(2, 0, 2, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI Semibold", 10F);
        _statusLabel.ForeColor = Ink;
        _statusLabel.Text = "Готов к анализу";

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.ForeColor = Muted;
        _summaryLabel.Text = "Риски: 0";

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Margin = new Padding(0, 14, 0, 14);
        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_summaryLabel, 1, 0);
        footer.Controls.Add(_progressBar, 2, 0);
        return footer;
    }

    private async Task StartScanAsync(bool deepScan)
    {
        SetScanState(true);
        _findingsGrid.Rows.Clear();
        _logList.Items.Clear();
        _progressBar.Value = 0;
        ResetMetricTiles();
        UpdateHealth("Анализ выполняется", deepScan ? "Глубокая проверка может занять несколько минут" : "Проверяем ключевые области системы");
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
            UpdateHealth("Проверка остановлена", "Результаты могут быть неполными");
            AddLog("Сканирование остановлено пользователем.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка анализа";
            UpdateHealth("Ошибка анализа", "Проверка завершилась с ошибкой");
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
            var rowIndex = _findingsGrid.Rows.Add(ToRussianRisk(finding.Risk), finding.Category, finding.Title, finding.Location);
            var row = _findingsGrid.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = finding.Risk switch
            {
                RiskLevel.High => Color.FromArgb(255, 240, 240),
                RiskLevel.Medium => Color.FromArgb(255, 249, 232),
                RiskLevel.Low => Color.FromArgb(236, 247, 255),
                _ => Surface
            };
            row.DefaultCellStyle.ForeColor = Ink;
        }

        _highCard.Value = summary.HighCount.ToString();
        _mediumCard.Value = summary.MediumCount.ToString();
        _lowCard.Value = summary.LowCount.ToString();
        _filesCard.Value = summary.FilesChecked.ToString();

        if (summary.HighCount > 0)
        {
            UpdateHealth("Требуется проверка", "Найдены элементы с высоким риском");
        }
        else if (summary.MediumCount > 0)
        {
            UpdateHealth("Есть замечания", "Стоит просмотреть найденные элементы");
        }
        else
        {
            UpdateHealth("Серьезных рисков нет", "Критичных находок не обнаружено");
        }

        _statusLabel.Text = $"Завершено за {(summary.FinishedAt - summary.StartedAt).TotalSeconds:0} сек.";
        _summaryLabel.Text = $"Высокий: {summary.HighCount}   Средний: {summary.MediumCount}   Низкий: {summary.LowCount}   Файлов: {summary.FilesChecked}";
        _exportButton.Enabled = true;
    }

    private void ResetMetricTiles()
    {
        _highCard.Value = "0";
        _mediumCard.Value = "0";
        _lowCard.Value = "0";
        _filesCard.Value = "0";
        _summaryLabel.Text = "Риски: 0";
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

    private void UpdateHealth(string title, string subtitle)
    {
        _healthLabel.Text = title;
        _healthCaptionLabel.Text = subtitle;
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

    private static string ToRussianRisk(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.High => "Высокий",
            RiskLevel.Medium => "Средний",
            RiskLevel.Low => "Низкий",
            _ => "Инфо"
        };
    }

    private sealed class ActionButton : Button
    {
        private readonly Color _baseColor;

        public ActionButton(string text, Color color)
        {
            _baseColor = color;
            Text = text;
            Width = 142;
            Height = 44;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = color;
            ForeColor = Color.White;
            Font = new Font("Segoe UI Semibold", 9.3F);
            Margin = new Padding(0, 0, 10, 0);
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            BackColor = Enabled ? _baseColor : Color.FromArgb(174, 184, 197);
            ForeColor = Enabled ? Color.White : Color.FromArgb(235, 239, 244);
        }
    }

    private sealed class MetricCard : RoundedPanel
    {
        private readonly Label _value = new();

        [Browsable(false)]

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public string Value

        {
            get => _value.Text;
            set => _value.Text = value;
        }

        public MetricCard(string title, Color accent)
        {
            Dock = DockStyle.Fill;
            BackColor = Surface;
            Radius = 8;
            Margin = new Padding(0, 0, 8, 0);
            Padding = new Padding(16, 12, 12, 10);
            Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
            Controls.Add(new Label
            {
                AutoSize = true,
                Text = title,
                Location = new Point(18, 13),
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.3F)
            });
            _value.AutoSize = true;
            _value.Text = "0";
            _value.Location = new Point(17, 37);
            _value.Font = new Font("Segoe UI Semibold", 21F);
            _value.ForeColor = Ink;
            Controls.Add(_value);
        }
    }

    private class RoundedPanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 8;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            var diameter = Radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
            Region = new Region(path);
        }
    }
}




