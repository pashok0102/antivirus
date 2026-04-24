using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace PcGuardian;

public sealed class MainForm : Form
{
    private static readonly Color LightBackground = Color.FromArgb(232, 238, 246);
    private static readonly Color LightSurface = Color.FromArgb(247, 250, 252);
    private static readonly Color LightSurfaceAlt = Color.FromArgb(240, 245, 250);
    private static readonly Color LightText = Color.FromArgb(30, 41, 59);
    private static readonly Color LightMuted = Color.FromArgb(100, 116, 139);

    private static readonly Color DarkBackground = Color.FromArgb(16, 23, 35);
    private static readonly Color DarkSurface = Color.FromArgb(24, 34, 52);
    private static readonly Color DarkSurfaceAlt = Color.FromArgb(37, 51, 76);
    private static readonly Color DarkText = Color.FromArgb(241, 245, 249);
    private static readonly Color DarkMuted = Color.FromArgb(166, 180, 200);

    private static readonly Color Cyan = Color.FromArgb(38, 185, 193);
    private static readonly Color Blue = Color.FromArgb(58, 120, 204);
    private static readonly Color Green = Color.FromArgb(31, 143, 105);
    private static readonly Color Red = Color.FromArgb(208, 92, 92);
    private static readonly Color Amber = Color.FromArgb(214, 152, 52);
    private static readonly Color Slate = Color.FromArgb(104, 116, 138);

    private readonly ActionButton _quickScanButton = new("Быстрый анализ", Blue);
    private readonly ActionButton _deepScanButton = new("Глубокий анализ", Green);
    private readonly ActionButton _cancelButton = new("Остановить", Slate);
    private readonly ActionButton _exportButton = new("Сохранить отчет", Slate);

    private readonly MetricCard _highCard = new("Высокий", Red);
    private readonly MetricCard _mediumCard = new("Средний", Amber);
    private readonly MetricCard _lowCard = new("Низкий", Blue);
    private readonly MetricCard _filesCard = new("Файлов", Slate);

    private readonly Label _healthTitle = new();
    private readonly Label _healthSubtitle = new();
    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly ProgressBar _progressBar = new();

    private readonly DataGridView _findingsGrid = new();
    private readonly ListBox _logList = new();
    private readonly ListBox _recommendationsList = new();
    private readonly DataGridView _historyGrid = new();
    private readonly CheckBox _recommendationsOnlyCheck = new();
    private readonly CheckBox _darkThemeCheck = new();

    private readonly Label _detailTitle = new();
    private readonly Label _detailRisk = new();
    private readonly Label _detailLocation = new();
    private readonly Label _detailWhy = new();
    private readonly Label _detailDanger = new();
    private readonly Label _detailAction = new();
    private readonly Label _detailIgnore = new();

    private readonly List<Button> _menuButtons = [];
    private readonly List<Label> _detailAccentLabels = [];
    private readonly List<Label> _detailBodyLabels = [];

    private Panel? _sidebarHost;
    private Panel? _contentHost;
    private TableLayoutPanel? _rootLayout;
    private TableLayoutPanel? _dashboardLayout;
    private TableLayoutPanel? _overviewLayout;
    private TableLayoutPanel? _findingsLayout;
    private FlowLayoutPanel? _metricsLayout;
    private Control? _overviewPage;
    private Control? _recommendationsPage;
    private Control? _historyPage;
    private Control? _settingsPage;
    private RoundedPanel? _detailCard;

    private List<SecurityFinding> _visibleFindings = [];
    private List<ScanHistoryEntry> _history = [];
    private CancellationTokenSource? _scanCancellation;
    private ScanSummary? _lastSummary;
    private bool _darkTheme;

    public MainForm()
    {
        Text = "PC Guardian";
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(1180, 760);
        MaximumSize = Screen.PrimaryScreen?.WorkingArea.Size ?? Size.Empty;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = true;
        MinimizeBox = true;
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Shield;

        _history = LoadHistory();

        BuildInterface();
        WireEvents();
        ApplyTheme();
        ResetMetricTiles();
        RefreshHistoryGrid();
        RefreshRecommendations(null);
        ShowRiskDetails(null);
        ShowOverview();
        UpdateResponsiveLayout();
    }

    private void WireEvents()
    {
        Resize += (_, _) => UpdateResponsiveLayout();
        Shown += (_, _) => UpdateResponsiveLayout();

        _quickScanButton.Click += async (_, _) => await StartScanAsync(false);
        _deepScanButton.Click += async (_, _) => await StartScanAsync(true);
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();
        _exportButton.Click += (_, _) => ExportReport();

        _recommendationsOnlyCheck.CheckedChanged += (_, _) =>
        {
            if (_recommendationsOnlyCheck.Checked)
            {
                ShowPage(_recommendationsPage, 1);
            }
        };

        _darkThemeCheck.CheckedChanged += (_, _) =>
        {
            _darkTheme = _darkThemeCheck.Checked;
            ApplyTheme();
        };

        _findingsGrid.SelectionChanged += (_, _) =>
        {
            if (_findingsGrid.SelectedRows.Count == 0)
            {
                ShowRiskDetails(null);
                return;
            }

            var index = _findingsGrid.SelectedRows[0].Index;
            ShowRiskDetails(index >= 0 && index < _visibleFindings.Count ? _visibleFindings[index] : null);
        };
    }

    private void BuildInterface()
    {
        Controls.Clear();

        _sidebarHost = new Panel { BackColor = Color.Transparent };
        _contentHost = new Panel { BackColor = Color.Transparent };
        Controls.Add(_contentHost);
        Controls.Add(_sidebarHost);

        _sidebarHost.Controls.Add(CreateSidebar());

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        _contentHost.Controls.Add(_rootLayout);

        _rootLayout.Controls.Add(CreateHeader(), 0, 0);
        _rootLayout.Controls.Add(CreateDashboard(), 0, 1);
        _rootLayout.Controls.Add(CreatePageHost(), 0, 2);
        _rootLayout.Controls.Add(CreateFooter(), 0, 3);
    }

    private Control CreateSidebar()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 10,
            Padding = new Padding(16)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(layout);

        var brand = new Panel { Dock = DockStyle.Fill };
        brand.Controls.Add(new PictureBox
        {
            Image = Icon?.ToBitmap() ?? SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(46, 46),
            Location = new Point(2, 8)
        });
        brand.Controls.Add(new Label
        {
            Text = "PC Guardian",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15.5F),
            Location = new Point(0, 60)
        });
        brand.Controls.Add(new Label
        {
            Text = "Security analyzer",
            AutoSize = true,
            ForeColor = DarkMuted,
            Font = new Font("Segoe UI", 9.3F),
            Location = new Point(2, 90)
        });
        layout.Controls.Add(brand, 0, 0);

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };
        menu.Controls.Add(CreateMenuButton("Обзор", () => ShowOverview()));
        menu.Controls.Add(CreateMenuButton("Рекомендации", () => ShowPage(_recommendationsPage, 1)));
        menu.Controls.Add(CreateMenuButton("История", () => ShowPage(_historyPage, 2)));
        menu.Controls.Add(CreateMenuButton("Настройки", () => ShowPage(_settingsPage, 3)));
        layout.Controls.Add(menu, 0, 1);

        var hintCard = new RoundedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            Radius = 8,
            Padding = new Padding(12)
        };
        hintCard.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Подробности риска открываются при выборе строки в таблице.",
            ForeColor = Color.FromArgb(205, 216, 232),
            Font = new Font("Segoe UI", 8.8F)
        });
        layout.Controls.Add(hintCard, 0, 2);

        return card;
    }

    private Button CreateMenuButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 174,
            Height = 42,
            Margin = new Padding(0, 0, 0, 8),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => action();
        _menuButtons.Add(button);
        return button;
    }

    private Control CreateHeader()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 10,
            Padding = new Padding(28, 22, 28, 22),
            Margin = new Padding(0, 0, 0, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        card.Controls.Add(layout);

        var brand = new Panel { Dock = DockStyle.Fill };
        brand.Controls.Add(new PictureBox
        {
            Image = Icon?.ToBitmap() ?? SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(56, 56),
            Location = new Point(0, 6)
        });
        brand.Controls.Add(new Label
        {
            Text = "PC Guardian",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 25F),
            Location = new Point(78, 2)
        });
        brand.Controls.Add(new Label
        {
            Text = "Desktop-анализатор безопасности компьютера",
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10.5F),
            Location = new Point(82, 56)
        });
        layout.Controls.Add(brand, 0, 0);

        var health = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 8,
            Padding = new Padding(18, 12, 18, 12),
            Margin = new Padding(14, 0, 0, 0)
        };
        _healthTitle.Dock = DockStyle.Top;
        _healthTitle.Height = 32;
        _healthTitle.Font = new Font("Segoe UI Semibold", 14.5F);
        _healthTitle.ForeColor = Color.White;
        _healthSubtitle.Dock = DockStyle.Fill;
        _healthSubtitle.Font = new Font("Segoe UI", 9.4F);
        _healthSubtitle.ForeColor = Color.FromArgb(193, 204, 222);
        health.Controls.Add(_healthSubtitle);
        health.Controls.Add(_healthTitle);
        layout.Controls.Add(health, 1, 0);

        var line = new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Cyan };
        card.Controls.Add(line);
        return card;
    }

    private Control CreateDashboard()
    {
        _dashboardLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        _dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 452));

        var actionsCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 10,
            Padding = new Padding(18, 16, 18, 14),
            Margin = new Padding(0, 0, 14, 0)
        };

        var actionsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };
        _cancelButton.Enabled = false;
        _exportButton.Enabled = false;

        _recommendationsOnlyCheck.Text = "Только рекомендации";
        _recommendationsOnlyCheck.Width = 180;
        _recommendationsOnlyCheck.Height = 42;
        _recommendationsOnlyCheck.Margin = new Padding(8, 5, 0, 0);
        _recommendationsOnlyCheck.Font = new Font("Segoe UI Semibold", 9.3F);

        actionsLayout.Controls.AddRange([
            _quickScanButton,
            _deepScanButton,
            _cancelButton,
            _exportButton,
            _recommendationsOnlyCheck
        ]);
        actionsCard.Controls.Add(actionsLayout);

        var metricsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };
        _metricsLayout = metricsLayout;
        metricsLayout.Controls.AddRange([_highCard, _mediumCard, _lowCard, _filesCard]);

        _dashboardLayout.Controls.Add(actionsCard, 0, 0);
        _dashboardLayout.Controls.Add(metricsLayout, 1, 0);
        return _dashboardLayout;
    }

    private Control CreatePageHost()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _overviewPage = CreateOverviewPage();
        _recommendationsPage = CreateRecommendationsPage();
        _historyPage = CreateHistoryPage();
        _settingsPage = CreateSettingsPage();

        host.Controls.Add(_overviewPage);
        host.Controls.Add(_recommendationsPage);
        host.Controls.Add(_historyPage);
        host.Controls.Add(_settingsPage);
        return host;
    }

    private Control CreateOverviewPage()
    {
        _overviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        _overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        _overviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        _overviewLayout.Controls.Add(CreateFindingsArea(), 0, 0);
        _overviewLayout.Controls.Add(CreateLogArea(), 0, 1);
        return _overviewLayout;
    }

    private Control CreateFindingsArea()
    {
        var panel = CreateSectionPanel("Найденные риски", "Выберите строку, чтобы справа открыть подробности риска");

        _findingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        _findingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _findingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

        ConfigureFindingsGrid();
        _findingsLayout.Controls.Add(_findingsGrid, 0, 0);
        _findingsLayout.Controls.Add(CreateRiskDetailsPanel(), 1, 0);
        AttachSectionContent(panel, _findingsLayout);
        return panel;
    }

    private void ConfigureFindingsGrid()
    {
        _findingsGrid.Dock = DockStyle.Fill;
        _findingsGrid.AllowUserToAddRows = false;
        _findingsGrid.AllowUserToDeleteRows = false;
        _findingsGrid.ReadOnly = true;
        _findingsGrid.RowHeadersVisible = false;
        _findingsGrid.MultiSelect = false;
        _findingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _findingsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _findingsGrid.BorderStyle = BorderStyle.None;
        _findingsGrid.ColumnHeadersHeight = 38;
        _findingsGrid.RowTemplate.Height = 34;
        _findingsGrid.EnableHeadersVisualStyles = false;

        if (_findingsGrid.Columns.Count == 0)
        {
            _findingsGrid.Columns.Add("Risk", "Риск");
            _findingsGrid.Columns.Add("Category", "Категория");
            _findingsGrid.Columns.Add("Title", "Находка");
            _findingsGrid.Columns.Add("Location", "Расположение");
            _findingsGrid.Columns[0].FillWeight = 18;
            _findingsGrid.Columns[1].FillWeight = 22;
            _findingsGrid.Columns[2].FillWeight = 40;
            _findingsGrid.Columns[3].FillWeight = 62;
        }
    }

    private Control CreateRiskDetailsPanel()
    {
        _detailCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 10,
            Padding = new Padding(18),
            Margin = new Padding(16, 0, 0, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _detailCard.Controls.Add(layout);

        _detailTitle.Dock = DockStyle.Fill;
        _detailTitle.Font = new Font("Segoe UI Semibold", 18F);
        _detailRisk.Dock = DockStyle.Fill;
        _detailRisk.Font = new Font("Segoe UI Semibold", 10.5F);
        _detailLocation.Dock = DockStyle.Fill;
        _detailLocation.Font = new Font("Segoe UI", 9.3F);

        layout.Controls.Add(_detailTitle, 0, 0);
        layout.Controls.Add(_detailRisk, 0, 1);
        layout.Controls.Add(_detailLocation, 0, 2);
        layout.Controls.Add(CreateDetailBlock("Почему найдено", _detailWhy), 0, 3);
        layout.Controls.Add(CreateDetailBlock("Чем опасно", _detailDanger), 0, 4);
        layout.Controls.Add(CreateDetailBlock("Что делать", _detailAction), 0, 5);
        layout.Controls.Add(CreateDetailBlock("Можно игнорировать", _detailIgnore), 0, 6);

        return _detailCard;
    }

    private Control CreateDetailBlock(string title, Label body)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI Semibold", 9.3F)
        };
        var bodyLabel = body;
        bodyLabel.Dock = DockStyle.Fill;
        bodyLabel.Font = new Font("Segoe UI", 8.9F);
        bodyLabel.AutoEllipsis = true;
        _detailAccentLabels.Add(titleLabel);
        _detailBodyLabels.Add(bodyLabel);
        panel.Controls.Add(bodyLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private Control CreateLogArea()
    {
        var panel = CreateSectionPanel("Журнал анализа", "Ход проверки и важные события");
        _logList.Dock = DockStyle.Fill;
        _logList.BorderStyle = BorderStyle.None;
        _logList.Font = new Font("Consolas", 9.4F);
        AttachSectionContent(panel, _logList);
        return panel;
    }

    private Control CreateRecommendationsPage()
    {
        var panel = CreateSectionPanel("Рекомендации", "Что можно сделать после проверки");
        _recommendationsList.Dock = DockStyle.Fill;
        _recommendationsList.BorderStyle = BorderStyle.None;
        _recommendationsList.Font = new Font("Segoe UI", 10F);
        AttachSectionContent(panel, _recommendationsList);
        return panel;
    }

    private Control CreateHistoryPage()
    {
        var panel = CreateSectionPanel("История проверок", "Сохраненные результаты прошлых запусков");
        _historyGrid.Dock = DockStyle.Fill;
        _historyGrid.AllowUserToAddRows = false;
        _historyGrid.AllowUserToDeleteRows = false;
        _historyGrid.ReadOnly = true;
        _historyGrid.RowHeadersVisible = false;
        _historyGrid.MultiSelect = false;
        _historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _historyGrid.BorderStyle = BorderStyle.None;
        _historyGrid.ColumnHeadersHeight = 38;
        _historyGrid.RowTemplate.Height = 34;
        _historyGrid.EnableHeadersVisualStyles = false;
        if (_historyGrid.Columns.Count == 0)
        {
            _historyGrid.Columns.Add("Date", "Дата");
            _historyGrid.Columns.Add("Risks", "Рисков");
            _historyGrid.Columns.Add("Files", "Файлов");
            _historyGrid.Columns.Add("Change", "Изменение");
        }
        AttachSectionContent(panel, _historyGrid);
        return panel;
    }

    private Control CreateSettingsPage()
    {
        var panel = CreateSectionPanel("Настройки", "Вид приложения и поведение после анализа");

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _darkThemeCheck.Text = "Темная тема";
        _darkThemeCheck.AutoSize = true;
        _darkThemeCheck.Font = new Font("Segoe UI Semibold", 10F);
        _darkThemeCheck.Margin = new Padding(0, 6, 0, 0);

        var note1 = new Label
        {
            Text = "Обычное окно зафиксировано по размеру. Развернуть можно только кнопкой сверху.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.6F)
        };
        var note2 = new Label
        {
            Text = "История проверок хранится локально и помогает видеть, стало лучше или хуже.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.6F)
        };

        content.Controls.Add(_darkThemeCheck, 0, 0);
        content.Controls.Add(note1, 0, 1);
        content.Controls.Add(note2, 0, 2);
        AttachSectionContent(panel, content);
        return panel;
    }

    private Control CreateFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI Semibold", 10F);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleCenter;
        _summaryLabel.Font = new Font("Segoe UI", 10F);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Margin = new Padding(0, 10, 0, 10);

        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_summaryLabel, 1, 0);
        footer.Controls.Add(_progressBar, 2, 0);
        return footer;
    }

    private RoundedPanel CreateSectionPanel(string title, string subtitle)
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 10,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F),
            Location = new Point(0, 0)
        });
        header.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.6F),
            Location = new Point(2, 30)
        });
        layout.Controls.Add(header, 0, 0);
        return panel;
    }

    private static void AttachSectionContent(Control panel, Control content)
    {
        if (panel.Controls.Count == 0 || panel.Controls[0] is not TableLayoutPanel layout)
        {
            return;
        }

        content.Margin = new Padding(0);
        layout.Controls.Add(content, 0, 1);
    }

    private async Task StartScanAsync(bool deep)
    {
        if (_scanCancellation is not null)
        {
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        SetScanState(true, deep ? "Выполняется глубокий анализ..." : "Выполняется быстрый анализ...");
        _logList.Items.Clear();
        _progressBar.Value = 0;

        var scanner = new SecurityScanner(
            new Progress<string>(message => _logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}")),
            new Progress<int>(value => _progressBar.Value = Math.Max(0, Math.Min(100, value))));

        try
        {
            var summary = deep
                ? await scanner.RunDeepScanAsync(_scanCancellation.Token)
                : await scanner.RunQuickScanAsync(_scanCancellation.Token);

            _lastSummary = summary;
            PopulateFindings(summary);
            UpdateMetrics(summary);
            RefreshRecommendations(summary);
            AddHistory(summary);
            UpdateHealth(summary);
            SetScanState(false, $"Завершено за {(summary.FinishedAt - summary.StartedAt).TotalSeconds:0} сек.");
            _exportButton.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            _logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Проверка остановлена пользователем.");
            SetScanState(false, "Проверка остановлена");
        }
        catch (Exception ex)
        {
            _logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Ошибка: {ex.Message}");
            SetScanState(false, "Произошла ошибка при анализе");
        }
        finally
        {
            _scanCancellation?.Dispose();
            _scanCancellation = null;
            _cancelButton.Enabled = false;
        }
    }

    private void PopulateFindings(ScanSummary summary)
    {
        _findingsGrid.Rows.Clear();
        _visibleFindings = summary.Findings.ToList();

        foreach (var finding in _visibleFindings)
        {
            _findingsGrid.Rows.Add(ToRiskText(finding.Risk), finding.Category, finding.Title, finding.Location);
        }

        if (_findingsGrid.Rows.Count > 0)
        {
            _findingsGrid.Rows[0].Selected = true;
            ShowRiskDetails(_visibleFindings[0]);
        }
        else
        {
            ShowRiskDetails(null);
        }
    }

    private void ShowRiskDetails(SecurityFinding? finding)
    {
        if (finding is null)
        {
            _detailTitle.Text = "Выберите находку";
            _detailRisk.Text = "Подробности появятся здесь";
            _detailLocation.Text = string.Empty;
            _detailWhy.Text = "Выберите строку в таблице, чтобы открыть пояснение.";
            _detailDanger.Text = "Здесь будет краткая оценка влияния на безопасность.";
            _detailAction.Text = "Здесь появятся шаги, что делать дальше.";
            _detailIgnore.Text = "Если риск низкий и источник понятен, его можно оставить.";
            return;
        }

        _detailTitle.Text = finding.Title;
        _detailRisk.Text = $"{ToRiskText(finding.Risk)} риск • {finding.Category}";
        _detailLocation.Text = finding.Location;
        _detailWhy.Text = BuildWhyText(finding);
        _detailDanger.Text = BuildDangerText(finding);
        _detailAction.Text = BuildActionText(finding);
        _detailIgnore.Text = BuildIgnoreText(finding);
    }

    private void RefreshRecommendations(ScanSummary? summary)
    {
        _recommendationsList.Items.Clear();

        if (summary is null)
        {
            _recommendationsList.Items.Add("После первой проверки здесь появятся рекомендации по системе.");
            return;
        }

        if (summary.HighCount > 0)
        {
            _recommendationsList.Items.Add("Сначала разберите все высокие риски и проверьте происхождение подозрительных файлов.");
        }

        if (summary.MediumCount > 0)
        {
            _recommendationsList.Items.Add("Посмотрите процессы и автозагрузку из пользовательских папок — там чаще всего прячутся лишние вещи.");
        }

        if (summary.TempBytes > 2L * 1024 * 1024 * 1024)
        {
            _recommendationsList.Items.Add("Очистите временные файлы: это не вирус, но системе станет заметно легче.");
        }

        if (summary.Findings.Any(f => f.Category == "Автозагрузка"))
        {
            _recommendationsList.Items.Add("Отключите из автозагрузки то, чем не пользуетесь каждый день.");
        }

        if (_recommendationsList.Items.Count == 0)
        {
            _recommendationsList.Items.Add("Серьезных рекомендаций нет. Состояние выглядит спокойно.");
        }
    }

    private void RefreshHistoryGrid()
    {
        _historyGrid.Rows.Clear();
        foreach (var item in _history.OrderByDescending(x => x.FinishedAt))
        {
            _historyGrid.Rows.Add(
                item.FinishedAt.ToString("dd.MM.yyyy HH:mm"),
                item.TotalRisks,
                item.FilesChecked,
                item.Change);
        }
    }

    private void AddHistory(ScanSummary summary)
    {
        var total = summary.HighCount + summary.MediumCount + summary.LowCount;
        var previous = _history.OrderByDescending(x => x.FinishedAt).FirstOrDefault();
        var change = previous is null
            ? "Первая сохраненная проверка"
            : total == previous.TotalRisks
                ? "Без изменений"
                : total < previous.TotalRisks
                    ? $"Лучше на {previous.TotalRisks - total}"
                    : $"Хуже на {total - previous.TotalRisks}";

        _history.Add(new ScanHistoryEntry(summary.FinishedAt, summary.HighCount, summary.MediumCount, summary.LowCount, summary.FilesChecked, total, change));
        _history = _history.OrderByDescending(x => x.FinishedAt).Take(40).ToList();
        SaveHistory();
        RefreshHistoryGrid();
    }

    private void UpdateMetrics(ScanSummary summary)
    {
        _highCard.Value = summary.HighCount.ToString();
        _mediumCard.Value = summary.MediumCount.ToString();
        _lowCard.Value = summary.LowCount.ToString();
        _filesCard.Value = summary.FilesChecked.ToString();
        _summaryLabel.Text = $"Риски: {summary.HighCount + summary.MediumCount + summary.LowCount}";
    }

    private void ResetMetricTiles()
    {
        _highCard.Value = "0";
        _mediumCard.Value = "0";
        _lowCard.Value = "0";
        _filesCard.Value = "0";
        _statusLabel.Text = "Готов к анализу";
        _summaryLabel.Text = "Риски: 0";
        _progressBar.Value = 0;
        UpdateHealth(null);
    }

    private void SetScanState(bool isScanning, string statusText)
    {
        _statusLabel.Text = statusText;
        _quickScanButton.Enabled = !isScanning;
        _deepScanButton.Enabled = !isScanning;
        _cancelButton.Enabled = isScanning;
        _recommendationsOnlyCheck.Enabled = !isScanning;
    }

    private void UpdateHealth(ScanSummary? summary)
    {
        if (summary is null)
        {
            _healthTitle.Text = "Готов к проверке";
            _healthSubtitle.Text = "Запустите анализ, чтобы оценить состояние системы.";
            return;
        }

        var total = summary.HighCount + summary.MediumCount + summary.LowCount;
        if (summary.HighCount > 0)
        {
            _healthTitle.Text = "Нужно внимание";
            _healthSubtitle.Text = $"Есть высокие риски: {summary.HighCount}. Начните именно с них.";
        }
        else if (summary.MediumCount > 0)
        {
            _healthTitle.Text = "Состояние среднее";
            _healthSubtitle.Text = $"Найдено {summary.MediumCount} средних и {summary.LowCount} низких рисков.";
        }
        else if (total > 0)
        {
            _healthTitle.Text = "Почти чисто";
            _healthSubtitle.Text = $"Остались только низкие замечания: {summary.LowCount}.";
        }
        else
        {
            _healthTitle.Text = "Все спокойно";
            _healthSubtitle.Text = "Серьезных замечаний не найдено.";
        }
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
            Filter = "Text file (*.txt)|*.txt",
            FileName = $"pc-guardian-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, SecurityScanner.BuildReport(_lastSummary));
        _logList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Отчет сохранен: {dialog.FileName}");
    }

    private void ShowOverview()
    {
        ShowPage(_overviewPage, 0);
    }

    private void ShowPage(Control? page, int menuIndex)
    {
        if (page is null)
        {
            return;
        }

        foreach (var control in new[] { _overviewPage, _recommendationsPage, _historyPage, _settingsPage })
        {
            if (control is not null)
            {
                control.Visible = ReferenceEquals(control, page);
            }
        }

        for (var i = 0; i < _menuButtons.Count; i++)
        {
            _menuButtons[i].BackColor = i == menuIndex ? Cyan : DarkSurface;
            _menuButtons[i].ForeColor = Color.White;
        }
    }

    private void UpdateResponsiveLayout()
    {
        var isMaximized = WindowState == FormWindowState.Maximized;
        var outerPadding = isMaximized ? 24 : 18;
        var sidebarWidth = isMaximized ? 226 : 214;
        var gap = 16;

        var client = ClientRectangle;
        _sidebarHost?.SetBounds(outerPadding, outerPadding, sidebarWidth, client.Height - outerPadding * 2);
        _contentHost?.SetBounds(outerPadding + sidebarWidth + gap, outerPadding, client.Width - sidebarWidth - gap - outerPadding * 2, client.Height - outerPadding * 2);

        if (_dashboardLayout is not null)
        {
            var metricsWidth = isMaximized ? 500 : 452;
            _dashboardLayout.ColumnStyles[1].Width = metricsWidth;
        }

        if (_metricsLayout is not null)
        {
            var width = isMaximized ? 112 : 100;
            var height = isMaximized ? 86 : 82;
            foreach (Control control in _metricsLayout.Controls)
            {
                control.Width = width;
                control.Height = height;
                control.Margin = new Padding(0, 0, 10, 0);
            }
        }

        if (_findingsLayout is not null)
        {
            _findingsLayout.ColumnStyles[1].Width = isMaximized ? 360 : 320;
        }
    }

    private void ApplyTheme()
    {
        var background = _darkTheme ? DarkBackground : LightBackground;
        var surface = _darkTheme ? DarkSurface : LightSurface;
        var surfaceAlt = _darkTheme ? DarkSurfaceAlt : LightSurfaceAlt;
        var text = _darkTheme ? DarkText : LightText;
        var muted = _darkTheme ? DarkMuted : LightMuted;

        BackColor = background;
        if (_contentHost is not null) _contentHost.BackColor = background;

        ApplyToRoundedPanels(this, surface, surfaceAlt, text, muted);
        ApplyGridTheme(_findingsGrid, surface, surfaceAlt, text, muted);
        ApplyGridTheme(_historyGrid, surface, surfaceAlt, text, muted);

        _logList.BackColor = _darkTheme ? Color.FromArgb(28, 39, 58) : Color.FromArgb(239, 244, 249);
        _logList.ForeColor = _darkTheme ? Color.FromArgb(226, 232, 240) : text;
        _recommendationsList.BackColor = _darkTheme ? Color.FromArgb(28, 39, 58) : Color.FromArgb(239, 244, 249);
        _recommendationsList.ForeColor = _darkTheme ? Color.FromArgb(226, 232, 240) : text;

        _statusLabel.ForeColor = _darkTheme ? Color.White : text;
        _summaryLabel.ForeColor = _darkTheme ? Color.FromArgb(196, 208, 224) : muted;

        _recommendationsOnlyCheck.ForeColor = _darkTheme ? Color.White : text;
        _darkThemeCheck.ForeColor = _darkTheme ? Color.White : text;

        _detailTitle.ForeColor = _darkTheme ? Color.FromArgb(244, 247, 250) : text;
        _detailRisk.ForeColor = _darkTheme ? Color.FromArgb(117, 173, 255) : Blue;
        _detailLocation.ForeColor = _darkTheme ? Color.FromArgb(185, 198, 219) : muted;

        foreach (var label in _detailAccentLabels)
        {
            label.ForeColor = _darkTheme ? Color.FromArgb(82, 210, 214) : Cyan;
        }

        foreach (var label in _detailBodyLabels)
        {
            label.ForeColor = _darkTheme ? Color.FromArgb(214, 223, 236) : text;
        }

        _healthTitle.ForeColor = Color.White;
        _healthSubtitle.ForeColor = Color.FromArgb(198, 210, 228);
    }

    private void ApplyToRoundedPanels(Control parent, Color surface, Color surfaceAlt, Color text, Color muted)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is RoundedPanel panel)
            {
                panel.BackColor = panel == _detailCard
                    ? (_darkTheme ? Color.FromArgb(31, 44, 66) : Color.FromArgb(240, 246, 252))
                    : surface;
            }
            else if (control is Panel p)
            {
                p.BackColor = Color.Transparent;
            }
            else if (control is TableLayoutPanel t)
            {
                t.BackColor = Color.Transparent;
            }
            else if (control is Label label)
            {
                if (label == _healthTitle || label == _healthSubtitle)
                {
                    // keep
                }
                else if (_detailAccentLabels.Contains(label))
                {
                    label.ForeColor = _darkTheme ? Color.FromArgb(82, 210, 214) : Cyan;
                }
                else if (_detailBodyLabels.Contains(label) || label == _detailTitle || label == _detailRisk || label == _detailLocation)
                {
                    // handled separately
                }
                else if (label.Font.Style.HasFlag(FontStyle.Bold) || label.Font.Name.Contains("Semibold", StringComparison.OrdinalIgnoreCase))
                {
                    label.ForeColor = text;
                }
                else
                {
                    label.ForeColor = muted;
                }
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = text;
            }

            ApplyToRoundedPanels(control, surface, surfaceAlt, text, muted);
        }

        if (_sidebarHost?.Controls.Count > 0 && _sidebarHost.Controls[0] is RoundedPanel sidebar)
        {
            sidebar.BackColor = DarkSurface;
            foreach (Control child in sidebar.Controls)
            {
                child.BackColor = DarkSurface;
            }
        }
    }

    private static void ApplyGridTheme(DataGridView grid, Color surface, Color surfaceAlt, Color text, Color muted)
    {
        grid.BackgroundColor = surface;
        grid.GridColor = Color.FromArgb(90, 105, 128);
        grid.DefaultCellStyle.BackColor = surface;
        grid.DefaultCellStyle.ForeColor = text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(46, 85, 128);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.BackColor = surfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
        grid.RowsDefaultCellStyle.BackColor = surface;
        grid.RowsDefaultCellStyle.ForeColor = text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = surfaceAlt;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = text;
    }

    private static string ToRiskText(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.High => "Высокий",
            RiskLevel.Medium => "Средний",
            RiskLevel.Low => "Низкий",
            _ => "Инфо"
        };
    }

    private static string BuildWhyText(SecurityFinding finding)
    {
        return finding.Details;
    }

    private static string BuildDangerText(SecurityFinding finding)
    {
        return finding.Risk switch
        {
            RiskLevel.High => "Такую находку лучше проверять сразу: она может ослаблять защиту или запускать подозрительные элементы автоматически.",
            RiskLevel.Medium => "Это не всегда вредоносно, но требует ручной проверки происхождения и назначения.",
            RiskLevel.Low => "Скорее всего влияние небольшое, но оно может замедлять систему или создавать лишний шум.",
            _ => "Информационная находка, полезна для понимания состояния системы."
        };
    }

    private static string BuildActionText(SecurityFinding finding)
    {
        if (finding.Category.Contains("Автозагрузка", StringComparison.OrdinalIgnoreCase))
        {
            return "Проверьте, нужен ли этот элемент при запуске Windows. Если нет — отключите его и посмотрите, исчезнет ли проблема.";
        }

        if (finding.Category.Contains("Процессы", StringComparison.OrdinalIgnoreCase))
        {
            return "Проверьте цифровую подпись, путь запуска и источник файла. Если программа незнакома — завершите процесс и изучите файл отдельно.";
        }

        if (finding.Category.Contains("Файлы", StringComparison.OrdinalIgnoreCase))
        {
            return "Проверьте происхождение файла. Если он появился не из понятного источника — удаляйте только после дополнительной проверки.";
        }

        return "Начните с проверки происхождения находки, затем решите, отключать ли ее, удалять или оставить как безопасную.";
    }

    private static string BuildIgnoreText(SecurityFinding finding)
    {
        return finding.Risk switch
        {
            RiskLevel.High => "Игнорировать стоит только если вы точно уверены в происхождении файла или настройки и понимаете, почему она здесь.",
            RiskLevel.Medium => "Можно игнорировать, если путь, программа и поведение вам знакомы и ожидаемы.",
            RiskLevel.Low => "Да, если это известный установщик, временный файл или обычный рабочий след программы.",
            _ => "Эту запись можно оставить как справочную, если она не мешает работе."
        };
    }

    private List<ScanHistoryEntry> LoadHistory()
    {
        try
        {
            var path = GetHistoryPath();
            if (!File.Exists(path))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<ScanHistoryEntry>>(File.ReadAllText(path)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveHistory()
    {
        try
        {
            var path = GetHistoryPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_history));
        }
        catch
        {
            // ignore local persistence errors
        }
    }

    private static string GetHistoryPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC Guardian", "scan-history.json");
    }

    private sealed record ScanHistoryEntry(DateTime FinishedAt, int High, int Medium, int Low, int FilesChecked, int TotalRisks, string Change);

    private sealed class ActionButton : Button
    {
        private readonly Color _baseColor;

        public ActionButton(string text, Color baseColor)
        {
            _baseColor = baseColor;
            Text = text;
            Width = 142;
            Height = 44;
            Margin = new Padding(0, 0, 10, 0);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            ForeColor = Color.White;
            Font = new Font("Segoe UI Semibold", 9.5F);
            BackColor = baseColor;
            UseVisualStyleBackColor = false;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            BackColor = Enabled ? _baseColor : Color.FromArgb(153, 166, 185);
            ForeColor = Enabled ? Color.White : Color.FromArgb(43, 52, 67);
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
            Radius = 8;
            Width = 100;
            Height = 82;
            Margin = new Padding(0, 0, 10, 0);
            Padding = new Padding(18, 13, 16, 12);

            Controls.Add(new Panel
            {
                Dock = DockStyle.Left,
                Width = 4,
                BackColor = accent
            });
            Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Location = new Point(18, 13),
                ForeColor = LightMuted,
                Font = new Font("Segoe UI", 9.3F)
            });

            _value.Text = "0";
            _value.Location = new Point(17, 37);
            _value.Font = new Font("Segoe UI Semibold", 21F);
            _value.ForeColor = LightText;
            _value.AutoSize = true;
            Controls.Add(_value);
        }
    }

    private class RoundedPanel : Panel
    {
        private int _radius = 10;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius
        {
            get => _radius;
            set
            {
                _radius = value;
                UpdateRegion();
                Invalidate();
            }
        }

        public RoundedPanel()
        {
            DoubleBuffered = true;
            Resize += (_, _) => UpdateRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(BackColor);
            using var path = BuildPath(ClientRectangle, _radius);
            e.Graphics.FillPath(brush, path);
            base.OnPaint(e);
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var path = BuildPath(new Rectangle(0, 0, Width, Height), _radius);
            Region = new Region(path);
        }

        private static GraphicsPath BuildPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(1, radius * 2);
            var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
