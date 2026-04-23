using System.ComponentModel;
using System.Text.Json;

namespace PcGuardian;

public sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(232, 238, 246);
    private static readonly Color Surface = Color.FromArgb(246, 249, 252);
    private static readonly Color SurfaceSoft = Color.FromArgb(239, 244, 249);
    private static readonly Color Ink = Color.FromArgb(31, 41, 55);
    private static readonly Color Muted = Color.FromArgb(96, 112, 132);
    private static readonly Color Navy = Color.FromArgb(24, 34, 52);
    private static readonly Color NavySoft = Color.FromArgb(35, 49, 72);
    private static readonly Color Cyan = Color.FromArgb(54, 166, 176);
    private static readonly Color Blue = Color.FromArgb(58, 120, 204);
    private static readonly Color Green = Color.FromArgb(34, 132, 98);
    private static readonly Color Red = Color.FromArgb(196, 88, 88);
    private static readonly Color Amber = Color.FromArgb(193, 136, 48);

    private readonly ActionButton _quickScanButton = new("Р‘С‹СЃС‚СЂС‹Р№ Р°РЅР°Р»РёР·", Blue);
    private readonly ActionButton _deepScanButton = new("Р“Р»СѓР±РѕРєРёР№ Р°РЅР°Р»РёР·", Green);
    private readonly ActionButton _cancelButton = new("РћСЃС‚Р°РЅРѕРІРёС‚СЊ", Red);
    private readonly ActionButton _exportButton = new("РЎРѕС…СЂР°РЅРёС‚СЊ РѕС‚С‡РµС‚", Color.FromArgb(73, 86, 107));

    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _healthLabel = new();
    private readonly Label _healthCaptionLabel = new();
    private readonly MetricCard _highCard = new("Р’С‹СЃРѕРєРёР№", Red);
    private readonly MetricCard _mediumCard = new("РЎСЂРµРґРЅРёР№", Amber);
    private readonly MetricCard _lowCard = new("РќРёР·РєРёР№", Blue);
    private readonly MetricCard _filesCard = new("Р¤Р°Р№Р»РѕРІ", Color.FromArgb(94, 107, 128));
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
    private readonly List<Control> _detailPanels = [];
    private readonly List<Label> _detailAccentLabels = [];

    private TableLayoutPanel? _mainLayout;
    private TableLayoutPanel? _shellLayout;
    private TableLayoutPanel? _headerLayout;
    private FlowLayoutPanel? _actionsLayout;
    private FlowLayoutPanel? _metricsLayout;
    private TableLayoutPanel? _findingsContentLayout;
    private RoundedPanel? _riskDetailsCard;
    private Control? _findingsArea;
    private Control? _logArea;
    private Control? _recommendationsArea;
    private Control? _historyArea;
    private Control? _settingsArea;
    private List<SecurityFinding> _visibleFindings = [];
    private List<ScanHistoryEntry> _history = [];
    private bool _darkTheme;

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
        _history = LoadHistory();
        BuildInterface();
        Resize += (_, _) => UpdateResponsiveLayout();
        Shown += (_, _) => UpdateResponsiveLayout();
    }

    private void BuildInterface()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Background
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 214));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _shellLayout = shell;
        Controls.Add(shell);

        shell.Controls.Add(CreateSidebar(), 0, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Background,
            Margin = new Padding(14, 0, 0, 0)
        };
        _mainLayout = root;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        shell.Controls.Add(root, 1, 0);

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateDashboard(), 0, 1);
        _findingsArea = CreateFindingsArea();
        _logArea = CreateLogArea();
        _recommendationsArea = CreateRecommendationsArea();
        _historyArea = CreateHistoryArea();
        _settingsArea = CreateSettingsArea();
        root.Controls.Add(_findingsArea, 0, 2);
        root.Controls.Add(_logArea, 0, 3);
        root.Controls.Add(CreateFooter(), 0, 4);

        ResetMetricTiles();
        UpdateHealth("РЎРёСЃС‚РµРјР° РЅРµ РїСЂРѕРІРµСЂСЏР»Р°СЃСЊ", "Р—Р°РїСѓСЃС‚РёС‚Рµ Р±С‹СЃС‚СЂС‹Р№ РёР»Рё РіР»СѓР±РѕРєРёР№ Р°РЅР°Р»РёР·");
        RefreshHistoryGrid();
        RefreshRecommendations(null);
        ShowRiskDetails(null);
        SelectMenu(0);
    }

    private Control CreateSidebar()
    {
        var sidebar = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Navy,
            Radius = 8,
            Padding = new Padding(14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Navy
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 275));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.Controls.Add(layout);

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Navy };
        brand.Controls.Add(new PictureBox
        {
            Image = Icon?.ToBitmap() ?? SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(46, 46),
            Location = new Point(2, 8)
        });
        brand.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "PC Guardian",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 15.5F),
            Location = new Point(0, 62)
        });
        brand.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Security analyzer",
            ForeColor = Color.FromArgb(177, 192, 214),
            Font = new Font("Segoe UI", 9.3F),
            Location = new Point(2, 91)
        });
        layout.Controls.Add(brand, 0, 0);

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Navy
        };
        layout.Controls.Add(menu, 0, 1);
        menu.Controls.Add(CreateMenuButton("РћР±Р·РѕСЂ", () => ShowMainScreen(0)));
        menu.Controls.Add(CreateMenuButton("Р РёСЃРєРё", () => ShowMainScreen(1)));
        menu.Controls.Add(CreateMenuButton("Р РµРєРѕРјРµРЅРґР°С†РёРё", () => ShowSingleScreen(_recommendationsArea, 2)));
        menu.Controls.Add(CreateMenuButton("РСЃС‚РѕСЂРёСЏ", () => ShowSingleScreen(_historyArea, 3)));
        menu.Controls.Add(CreateMenuButton("РќР°СЃС‚СЂРѕР№РєРё", () => ShowSingleScreen(_settingsArea, 3)));

        var hint = new RoundedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 88,
            BackColor = NavySoft,
            Radius = 8,
            Padding = new Padding(12)
        };
        hint.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "РџРѕРґСЂРѕР±РЅРѕСЃС‚Рё СЂРёСЃРєР° РѕС‚РєСЂС‹РІР°СЋС‚СЃСЏ РїСЂРё РІС‹Р±РѕСЂРµ СЃС‚СЂРѕРєРё РІ С‚Р°Р±Р»РёС†Рµ.",
            ForeColor = Color.FromArgb(202, 213, 226),
            Font = new Font("Segoe UI", 8.7F)
        });
        layout.Controls.Add(hint, 0, 2);
        return sidebar;
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
            BackColor = Navy,
            ForeColor = Color.FromArgb(213, 225, 242),
            Font = new Font("Segoe UI Semibold", 9.5F),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => action();
        _menuButtons.Add(button);
        return button;
    }

    private void ShowMainScreen(int menuIndex)
    {
        if (_mainLayout is null || _findingsArea is null || _logArea is null)
        {
            return;
        }

        ClearScreenRows();
        _mainLayout.Controls.Add(_findingsArea, 0, 2);
        _mainLayout.SetRowSpan(_findingsArea, 1);
        _mainLayout.Controls.Add(_logArea, 0, 3);
        _mainLayout.SetRowSpan(_logArea, 1);
        SelectMenu(menuIndex);
    }

    private void ShowSingleScreen(Control? screen, int menuIndex)
    {
        if (_mainLayout is null || screen is null)
        {
            return;
        }

        ClearScreenRows();
        _mainLayout.Controls.Add(screen, 0, 2);
        _mainLayout.SetRowSpan(screen, 2);
        SelectMenu(menuIndex);
    }

    private void ClearScreenRows()
    {
        if (_mainLayout is null)
        {
            return;
        }

        foreach (var control in new[] { _findingsArea, _logArea, _recommendationsArea, _historyArea, _settingsArea })
        {
            if (control is not null && _mainLayout.Controls.Contains(control))
            {
                _mainLayout.Controls.Remove(control);
                _mainLayout.SetRowSpan(control, 1);
            }
        }
    }

    private void SelectMenu(int index)
    {
        for (var i = 0; i < _menuButtons.Count; i++)
        {
            _menuButtons[i].BackColor = i == index ? Cyan : Navy;
            _menuButtons[i].ForeColor = Color.White;
        }
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

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Navy
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _headerLayout = layout;
        header.Controls.Add(layout);

        var brandPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Navy
        };
        layout.Controls.Add(brandPanel, 0, 0);

        var shield = new PictureBox
        {
            Image = Icon?.ToBitmap() ?? SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(56, 56),
            Location = new Point(0, 6)
        };
        brandPanel.Controls.Add(shield);

        brandPanel.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 25F),
            Text = "PC Guardian",
            Location = new Point(78, 2)
        });

        brandPanel.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10.5F),
            Text = "Desktop-анализатор безопасности компьютера",
            Location = new Point(82, 56)
        });

        var healthPanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NavySoft,
            Radius = 8,
            Padding = new Padding(18, 10, 18, 10),
            Margin = new Padding(16, 0, 0, 0)
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
        layout.Controls.Add(healthPanel, 1, 0);

        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Cyan });
        return header;
    }
    private Control CreateDashboard()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Background,
            Margin = new Padding(0, 0, 0, 12)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        area.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        area.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

        var actionsCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Radius = 8,
            Padding = new Padding(18, 16, 18, 12)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            WrapContents = false,
            AutoScroll = true
        };

        _cancelButton.Enabled = false;
        _exportButton.Enabled = false;
        _quickScanButton.Click += async (_, _) => await StartScanAsync(false);
        _deepScanButton.Click += async (_, _) => await StartScanAsync(true);
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();
        _exportButton.Click += (_, _) => ExportReport();

        _recommendationsOnlyCheck.Text = "РўРѕР»СЊРєРѕ СЂРµРєРѕРјРµРЅРґР°С†РёРё";
        _recommendationsOnlyCheck.Width = 170;
        _recommendationsOnlyCheck.Height = 42;
        _recommendationsOnlyCheck.Margin = new Padding(10, 6, 0, 0);
        _recommendationsOnlyCheck.ForeColor = Ink;
        _recommendationsOnlyCheck.Font = new Font("Segoe UI Semibold", 9.3F);
        _recommendationsOnlyCheck.CheckedChanged += (_, _) =>
        {
            if (_recommendationsOnlyCheck.Checked && _lastSummary is not null)
            {
                ShowSingleScreen(_recommendationsArea, 1);
            }
        };

        _actionsLayout = actions;
        actions.Controls.AddRange([_quickScanButton, _deepScanButton, _cancelButton, _exportButton, _recommendationsOnlyCheck]);
        actionsCard.Controls.Add(actions);
        area.Controls.Add(actionsCard, 0, 0);

        var metrics = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Margin = new Padding(0),
            WrapContents = false,
            AutoScroll = true
        };
        _metricsLayout = metrics;
        metrics.Controls.Add(_highCard);
        metrics.Controls.Add(_mediumCard);
        metrics.Controls.Add(_lowCard);
        metrics.Controls.Add(_filesCard);
        area.Controls.Add(metrics, 0, 1);
        return area;
    }
    private Control CreateFindingsArea()
    {
        var panel = CreateSectionPanel("РќР°Р№РґРµРЅРЅС‹Рµ СЂРёСЃРєРё", "Р’С‹Р±РµСЂРёС‚Рµ СЃС‚СЂРѕРєСѓ, С‡С‚РѕР±С‹ СЃРїСЂР°РІР° РѕС‚РєСЂС‹С‚СЊ РїРѕРґСЂРѕР±РЅРѕСЃС‚Рё СЂРёСЃРєР°");
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Surface
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 338));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _findingsGrid.Dock = DockStyle.Fill;
        _findingsGrid.BackgroundColor = Surface;
        _findingsGrid.BorderStyle = BorderStyle.None;
        _findingsGrid.AllowUserToAddRows = false;
        _findingsGrid.ReadOnly = true;
        _findingsGrid.RowHeadersVisible = false;
        _findingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _findingsGrid.MultiSelect = false;
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
        if (_findingsGrid.Columns.Count == 0)
        {
            _findingsGrid.Columns.Add("Risk", "Р РёСЃРє");
            _findingsGrid.Columns.Add("Category", "РљР°С‚РµРіРѕСЂРёСЏ");
            _findingsGrid.Columns.Add("Title", "РќР°С…РѕРґРєР°");
            _findingsGrid.Columns.Add("Location", "Р Р°СЃРїРѕР»РѕР¶РµРЅРёРµ");
            _findingsGrid.Columns["Risk"]!.FillWeight = 18;
            _findingsGrid.Columns["Category"]!.FillWeight = 24;
            _findingsGrid.Columns["Title"]!.FillWeight = 44;
            _findingsGrid.Columns["Location"]!.FillWeight = 74;
            _findingsGrid.SelectionChanged += (_, _) =>
            {
                if (_findingsGrid.SelectedRows.Count == 0)
                {
                    return;
                }

                var index = _findingsGrid.SelectedRows[0].Index;
                ShowRiskDetails(index >= 0 && index < _visibleFindings.Count ? _visibleFindings[index] : null);
            };
        }

        _findingsContentLayout = content;
        content.Controls.Add(_findingsGrid, 0, 0);
        content.Controls.Add(CreateRiskDetailsPanel(), 1, 0);
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(content, 0, 1);
        return panel;
    }

    private Control CreateRiskDetailsPanel()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceSoft,
            Radius = 8,
            Padding = new Padding(16),
            Margin = new Padding(14, 0, 0, 0)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = SurfaceSoft
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        _riskDetailsCard = card;
        _detailPanels.Add(card);
        _detailPanels.Add(layout);
        card.Controls.Add(layout);

        _detailTitle.Dock = DockStyle.Fill;
        _detailTitle.Font = new Font("Segoe UI Semibold", 13F);
        _detailTitle.ForeColor = Ink;
        _detailTitle.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_detailTitle, 0, 0);

        _detailRisk.Dock = DockStyle.Fill;
        _detailRisk.Font = new Font("Segoe UI Semibold", 10F);
        _detailRisk.ForeColor = Blue;
        layout.Controls.Add(_detailRisk, 0, 1);

        _detailLocation.Dock = DockStyle.Fill;
        _detailLocation.Font = new Font("Segoe UI", 8.5F);
        _detailLocation.ForeColor = Muted;
        _detailLocation.AutoEllipsis = true;
        layout.Controls.Add(_detailLocation, 0, 2);

        layout.Controls.Add(CreateDetailBlock("РџРѕС‡РµРјСѓ РЅР°Р№РґРµРЅРѕ", _detailWhy), 0, 3);
        layout.Controls.Add(CreateDetailBlock("Р§РµРј РѕРїР°СЃРЅРѕ", _detailDanger), 0, 4);
        layout.Controls.Add(CreateDetailBlock("Р§С‚Рѕ РґРµР»Р°С‚СЊ", _detailAction), 0, 5);
        layout.Controls.Add(CreateDetailBlock("РњРѕР¶РЅРѕ РёРіРЅРѕСЂРёСЂРѕРІР°С‚СЊ", _detailIgnore), 0, 6);
        return card;
    }

    private Control CreateDetailBlock(string title, Label body)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceSoft, Margin = new Padding(0, 4, 0, 0) };
        _detailPanels.Add(panel);
        var accentLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 21,
            Text = title,
            ForeColor = Cyan,
            Font = new Font("Segoe UI Semibold", 9.2F)
        };
        _detailAccentLabels.Add(accentLabel);
        panel.Controls.Add(accentLabel);
        body.Dock = DockStyle.Fill;
        body.ForeColor = Ink;
        body.Font = new Font("Segoe UI", 9F);
        body.AutoEllipsis = true;
        panel.Controls.Add(body);
        return panel;
    }
    private Control CreateLogArea()
    {
        var panel = CreateSectionPanel("Р–СѓСЂРЅР°Р» Р°РЅР°Р»РёР·Р°", "РҐРѕРґ РїСЂРѕРІРµСЂРєРё Рё РІР°Р¶РЅС‹Рµ СЃРѕР±С‹С‚РёСЏ");
        _logList.Dock = DockStyle.Fill;
        _logList.BorderStyle = BorderStyle.None;
        _logList.BackColor = SurfaceSoft;
        _logList.ForeColor = Color.FromArgb(51, 65, 85);
        _logList.Font = new Font("Consolas", 9.5F);
        _logList.ItemHeight = 18;
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_logList, 0, 1);
        return panel;
    }

    private Control CreateRecommendationsArea()
    {
        var panel = CreateSectionPanel("Р РµРєРѕРјРµРЅРґР°С†РёРё", "РўРѕР»СЊРєРѕ РїРѕРЅСЏС‚РЅС‹Рµ РґРµР№СЃС‚РІРёСЏ РїРѕ РїРѕСЃР»РµРґРЅРµРјСѓ Р°РЅР°Р»РёР·Сѓ");
        _recommendationsList.Dock = DockStyle.Fill;
        _recommendationsList.BorderStyle = BorderStyle.None;
        _recommendationsList.BackColor = SurfaceSoft;
        _recommendationsList.ForeColor = Ink;
        _recommendationsList.Font = new Font("Segoe UI", 10.2F);
        _recommendationsList.ItemHeight = 28;
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_recommendationsList, 0, 1);
        return panel;
    }

    private Control CreateHistoryArea()
    {
        var panel = CreateSectionPanel("РСЃС‚РѕСЂРёСЏ РїСЂРѕРІРµСЂРѕРє", "Р”Р°С‚Р°, С‡РёСЃР»Рѕ СЂРёСЃРєРѕРІ Рё РёР·РјРµРЅРµРЅРёРµ РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ РїСЂРµРґС‹РґСѓС‰РµРіРѕ Р°РЅР°Р»РёР·Р°");
        _historyGrid.Dock = DockStyle.Fill;
        _historyGrid.BackgroundColor = Surface;
        _historyGrid.BorderStyle = BorderStyle.None;
        _historyGrid.AllowUserToAddRows = false;
        _historyGrid.ReadOnly = true;
        _historyGrid.RowHeadersVisible = false;
        _historyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _historyGrid.EnableHeadersVisualStyles = false;
        _historyGrid.GridColor = Color.FromArgb(226, 232, 240);
        _historyGrid.RowTemplate.Height = 36;
        _historyGrid.ColumnHeadersHeight = 38;
        if (_historyGrid.Columns.Count == 0)
        {
            _historyGrid.Columns.Add("Date", "Р”Р°С‚Р°");
            _historyGrid.Columns.Add("High", "Р’С‹СЃРѕРєРёР№");
            _historyGrid.Columns.Add("Medium", "РЎСЂРµРґРЅРёР№");
            _historyGrid.Columns.Add("Low", "РќРёР·РєРёР№");
            _historyGrid.Columns.Add("Files", "Р¤Р°Р№Р»РѕРІ");
            _historyGrid.Columns.Add("Change", "РР·РјРµРЅРµРЅРёРµ");
        }
        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(_historyGrid, 0, 1);
        return panel;
    }

    private Control CreateSettingsArea()
    {
        var panel = CreateSectionPanel("РќР°СЃС‚СЂРѕР№РєРё РёРЅС‚РµСЂС„РµР№СЃР°", "РўРµРјРЅР°СЏ С‚РµРјР° Рё СЂРµР¶РёРј РїРѕРєР°Р·Р° СЂРµРєРѕРјРµРЅРґР°С†РёР№");
        var body = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = SurfaceSoft,
            Padding = new Padding(18)
        };

        _darkThemeCheck.Text = "РўРµРјРЅР°СЏ С‚РµРјР°";
        _darkThemeCheck.Width = 240;
        _darkThemeCheck.Height = 38;
        _darkThemeCheck.Font = new Font("Segoe UI Semibold", 10F);
        _darkThemeCheck.ForeColor = Ink;
        _darkThemeCheck.CheckedChanged += (_, _) => ApplyTheme(_darkThemeCheck.Checked);
        body.Controls.Add(_darkThemeCheck);

        body.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Text = "Р•СЃР»Рё РІРєР»СЋС‡РёС‚СЊ 'РўРѕР»СЊРєРѕ СЂРµРєРѕРјРµРЅРґР°С†РёРё', РїРѕСЃР»Рµ СЃРєР°РЅРёСЂРѕРІР°РЅРёСЏ РїСЂРёР»РѕР¶РµРЅРёРµ СЃСЂР°Р·Сѓ РѕС‚РєСЂРѕРµС‚ СЌРєСЂР°РЅ СЃ РґРµР№СЃС‚РІРёСЏРјРё Р±РµР· РґР»РёРЅРЅРѕРіРѕ СЃРїРёСЃРєР° РЅР°С…РѕРґРѕРє.",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Margin = new Padding(0, 10, 0, 0)
        });

        ((TableLayoutPanel)panel.Controls[0]).Controls.Add(body, 0, 1);
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
        _statusLabel.Text = "Р“РѕС‚РѕРІ Рє Р°РЅР°Р»РёР·Сѓ";

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _summaryLabel.ForeColor = Muted;
        _summaryLabel.Text = "Р РёСЃРєРё: 0";

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
        _visibleFindings = [];
        _findingsGrid.Rows.Clear();
        _logList.Items.Clear();
        _progressBar.Value = 0;
        ResetMetricTiles();
        ShowRiskDetails(null);
        UpdateHealth("РђРЅР°Р»РёР· РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ", deepScan ? "Р“Р»СѓР±РѕРєР°СЏ РїСЂРѕРІРµСЂРєР° РјРѕР¶РµС‚ Р·Р°РЅСЏС‚СЊ РЅРµСЃРєРѕР»СЊРєРѕ РјРёРЅСѓС‚" : "РџСЂРѕРІРµСЂСЏРµРј РєР»СЋС‡РµРІС‹Рµ РѕР±Р»Р°СЃС‚Рё СЃРёСЃС‚РµРјС‹");
        _statusLabel.Text = deepScan ? "РРґРµС‚ РіР»СѓР±РѕРєРёР№ Р°РЅР°Р»РёР·..." : "РРґРµС‚ Р±С‹СЃС‚СЂС‹Р№ Р°РЅР°Р»РёР·...";
        AddLog("РџСЂРёР»РѕР¶РµРЅРёРµ Р·Р°РїСѓС‰РµРЅРѕ СЃ РїСЂР°РІР°РјРё Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР°.");
        _scanCancellation = new CancellationTokenSource();
        var scanner = new SecurityScanner(new Progress<string>(AddLog), new Progress<int>(v => _progressBar.Value = Math.Clamp(v, 0, 100)));

        try
        {
            _lastSummary = deepScan
                ? await scanner.RunDeepScanAsync(_scanCancellation.Token)
                : await scanner.RunQuickScanAsync(_scanCancellation.Token);
            ShowSummary(_lastSummary);
            SaveHistory(_lastSummary);
            RefreshHistoryGrid();
            RefreshRecommendations(_lastSummary);
            if (_recommendationsOnlyCheck.Checked)
            {
                ShowSingleScreen(_recommendationsArea, 1);
            }
            AddLog("Р“РѕС‚РѕРІРѕ. РћС‚С‡РµС‚ РјРѕР¶РЅРѕ СЃРѕС…СЂР°РЅРёС‚СЊ РІ С„Р°Р№Р».");
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "РЎРєР°РЅРёСЂРѕРІР°РЅРёРµ РѕСЃС‚Р°РЅРѕРІР»РµРЅРѕ";
            UpdateHealth("РџСЂРѕРІРµСЂРєР° РѕСЃС‚Р°РЅРѕРІР»РµРЅР°", "Р РµР·СѓР»СЊС‚Р°С‚С‹ РјРѕРіСѓС‚ Р±С‹С‚СЊ РЅРµРїРѕР»РЅС‹РјРё");
            AddLog("РЎРєР°РЅРёСЂРѕРІР°РЅРёРµ РѕСЃС‚Р°РЅРѕРІР»РµРЅРѕ РїРѕР»СЊР·РѕРІР°С‚РµР»РµРј.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "РћС€РёР±РєР° Р°РЅР°Р»РёР·Р°";
            UpdateHealth("РћС€РёР±РєР° Р°РЅР°Р»РёР·Р°", "РџСЂРѕРІРµСЂРєР° Р·Р°РІРµСЂС€РёР»Р°СЃСЊ СЃ РѕС€РёР±РєРѕР№");
            AddLog($"РћС€РёР±РєР°: {ex.Message}");
            MessageBox.Show(ex.Message, "РћС€РёР±РєР° Р°РЅР°Р»РёР·Р°", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        _visibleFindings = summary.Findings.ToList();
        foreach (var finding in _visibleFindings)
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
            UpdateHealth("РўСЂРµР±СѓРµС‚СЃСЏ РїСЂРѕРІРµСЂРєР°", "РќР°Р№РґРµРЅС‹ СЌР»РµРјРµРЅС‚С‹ СЃ РІС‹СЃРѕРєРёРј СЂРёСЃРєРѕРј");
        }
        else if (summary.MediumCount > 0)
        {
            UpdateHealth("Р•СЃС‚СЊ Р·Р°РјРµС‡Р°РЅРёСЏ", "РЎС‚РѕРёС‚ РїСЂРѕСЃРјРѕС‚СЂРµС‚СЊ РЅР°Р№РґРµРЅРЅС‹Рµ СЌР»РµРјРµРЅС‚С‹");
        }
        else
        {
            UpdateHealth("РЎРµСЂСЊРµР·РЅС‹С… СЂРёСЃРєРѕРІ РЅРµС‚", "РљСЂРёС‚РёС‡РЅС‹С… РЅР°С…РѕРґРѕРє РЅРµ РѕР±РЅР°СЂСѓР¶РµРЅРѕ");
        }

        _statusLabel.Text = $"Р—Р°РІРµСЂС€РµРЅРѕ Р·Р° {(summary.FinishedAt - summary.StartedAt).TotalSeconds:0} СЃРµРє.";
        _summaryLabel.Text = $"Р’С‹СЃРѕРєРёР№: {summary.HighCount}   РЎСЂРµРґРЅРёР№: {summary.MediumCount}   РќРёР·РєРёР№: {summary.LowCount}   Р¤Р°Р№Р»РѕРІ: {summary.FilesChecked}";
        _exportButton.Enabled = true;

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
            _detailTitle.Text = "Р’С‹Р±РµСЂРёС‚Рµ РЅР°С…РѕРґРєСѓ";
            _detailRisk.Text = "РџРѕРґСЂРѕР±РЅРѕСЃС‚Рё РїРѕСЏРІСЏС‚СЃСЏ Р·РґРµСЃСЊ";
            _detailLocation.Text = string.Empty;
            _detailWhy.Text = "РџРѕСЃР»Рµ Р°РЅР°Р»РёР·Р° РЅР°Р¶РјРёС‚Рµ РЅР° СЃС‚СЂРѕРєСѓ РІ С‚Р°Р±Р»РёС†Рµ РЅР°Р№РґРµРЅРЅС‹С… СЂРёСЃРєРѕРІ.";
            _detailDanger.Text = "РџР°РЅРµР»СЊ РїРѕРєР°Р¶РµС‚, РЅР°СЃРєРѕР»СЊРєРѕ СЌС‚Рѕ РѕРїР°СЃРЅРѕ Рё С‚СЂРµР±СѓРµС‚ Р»Рё РґРµР№СЃС‚РІРёР№.";
            _detailAction.Text = "РЎРЅР°С‡Р°Р»Р° Р·Р°РїСѓСЃС‚РёС‚Рµ Р±С‹СЃС‚СЂС‹Р№ РёР»Рё РіР»СѓР±РѕРєРёР№ Р°РЅР°Р»РёР·.";
            _detailIgnore.Text = "РќРёР·РєРёРµ Р·Р°РјРµС‡Р°РЅРёСЏ РјРѕР¶РЅРѕ РёРіРЅРѕСЂРёСЂРѕРІР°С‚СЊ С‚РѕР»СЊРєРѕ РµСЃР»Рё РІС‹ СѓРІРµСЂРµРЅС‹ РІ РїСЂРѕРёСЃС…РѕР¶РґРµРЅРёРё С„Р°Р№Р»Р° РёР»Рё РїСЂРѕС†РµСЃСЃР°.";
            return;
        }

        _detailTitle.Text = finding.Title;
        _detailRisk.Text = $"{ToRussianRisk(finding.Risk)} СЂРёСЃРє В· {finding.Category}";
        _detailLocation.Text = finding.Location;
        _detailWhy.Text = string.IsNullOrWhiteSpace(finding.Details) ? "Р­Р»РµРјРµРЅС‚ СЃРѕРІРїР°Р» СЃ РїСЂР°РІРёР»РѕРј Р°РЅР°Р»РёР·Р° PC Guardian." : finding.Details;
        _detailDanger.Text = finding.Risk switch
        {
            RiskLevel.High => "РўР°РєСѓСЋ РЅР°С…РѕРґРєСѓ Р»СѓС‡С€Рµ РїСЂРѕРІРµСЂРёС‚СЊ РІ РїРµСЂРІСѓСЋ РѕС‡РµСЂРµРґСЊ: РѕРЅР° РјРѕР¶РµС‚ РІР»РёСЏС‚СЊ РЅР° Р·Р°С‰РёС‚Сѓ Windows, Р°РІС‚РѕР·Р°РіСЂСѓР·РєСѓ РёР»Рё Р·Р°РїСѓСЃРє РїРѕРґРѕР·СЂРёС‚РµР»СЊРЅРѕРіРѕ С„Р°Р№Р»Р°.",
            RiskLevel.Medium => "РЎСЂРµРґРЅРёР№ СЂРёСЃРє С‡Р°СЃС‚Рѕ СЃРІСЏР·Р°РЅ СЃ С„Р°Р№Р»Р°РјРё РёР· РїРѕР»СЊР·РѕРІР°С‚РµР»СЊСЃРєРёС… РїР°РїРѕРє, AppData РёР»Рё РІСЂРµРјРµРЅРЅС‹С… РєР°С‚Р°Р»РѕРіРѕРІ. Р­С‚Рѕ РЅРµ РІСЃРµРіРґР° РІРёСЂСѓСЃ, РЅРѕ С‚СЂРµР±СѓРµС‚ РїСЂРѕРІРµСЂРєРё.",
            RiskLevel.Low => "РќРёР·РєРёР№ СЂРёСЃРє РѕР±С‹С‡РЅРѕ РѕР·РЅР°С‡Р°РµС‚ РјСѓСЃРѕСЂ, РІСЂРµРјРµРЅРЅС‹Рµ С„Р°Р№Р»С‹ РёР»Рё СЃС‚Р°СЂС‹Рµ СѓСЃС‚Р°РЅРѕРІС‰РёРєРё. Р­С‚Рѕ СЃРєРѕСЂРµРµ РІРѕРїСЂРѕСЃ С‡РёСЃС‚РѕС‚С‹ СЃРёСЃС‚РµРјС‹, С‡РµРј Р·Р°СЂР°Р¶РµРЅРёСЏ.",
            _ => "РРЅС„РѕСЂРјР°С†РёРѕРЅРЅР°СЏ РЅР°С…РѕРґРєР° РґРѕР±Р°РІР»РµРЅР° РґР»СЏ РєРѕРЅС‚РµРєСЃС‚Р° Рё РЅРµ РІС‹РіР»СЏРґРёС‚ СЃСЂРѕС‡РЅРѕР№."
        };
        _detailAction.Text = BuildActionText(finding);
        _detailIgnore.Text = finding.Risk switch
        {
            RiskLevel.High => "РРіРЅРѕСЂРёСЂРѕРІР°С‚СЊ РЅРµ СЃС‚РѕРёС‚, РїРѕРєР° РІС‹ РЅРµ РїСЂРѕРІРµСЂРёР»Рё РёСЃС‚РѕС‡РЅРёРє Рё РїРѕРґРїРёСЃСЊ С„Р°Р№Р»Р°.",
            RiskLevel.Medium => "РњРѕР¶РЅРѕ РѕСЃС‚Р°РІРёС‚СЊ, РµСЃР»Рё РїСЂРѕРіСЂР°РјРјР° РІР°Рј Р·РЅР°РєРѕРјР° Рё РїСѓС‚СЊ РІС‹РіР»СЏРґРёС‚ РѕР¶РёРґР°РµРјРѕ.",
            _ => "Р”Р°, РµСЃР»Рё РІС‹ СѓРІРµСЂРµРЅС‹, С‡С‚Рѕ СЌС‚Рѕ РєСЌС€, СѓСЃС‚Р°РЅРѕРІС‰РёРє РёР»Рё С‡Р°СЃС‚СЊ РґРѕРІРµСЂРµРЅРЅРѕР№ РїСЂРѕРіСЂР°РјРјС‹."
        };
    }

    private static string BuildActionText(SecurityFinding finding)
    {
        if (finding.Category.Contains("РђРІС‚РѕР·Р°РіСЂСѓР·РєР°", StringComparison.OrdinalIgnoreCase))
        {
            return "РћС‚РєСЂРѕР№С‚Рµ Р°РІС‚РѕР·Р°РіСЂСѓР·РєСѓ Windows Рё РѕС‚РєР»СЋС‡РёС‚Рµ СЌР»РµРјРµРЅС‚, РµСЃР»Рё РїСЂРѕРіСЂР°РјРјР° РІР°Рј РЅРµР·РЅР°РєРѕРјР°. РџРµСЂРµРґ СѓРґР°Р»РµРЅРёРµРј РїСЂРѕРІРµСЂСЊС‚Рµ РїСѓС‚СЊ Рё С†РёС„СЂРѕРІСѓСЋ РїРѕРґРїРёСЃСЊ С„Р°Р№Р»Р°.";
        }

        if (finding.Category.Contains("РџСЂРѕС†РµСЃСЃС‹", StringComparison.OrdinalIgnoreCase))
        {
            return "РћС‚РєСЂРѕР№С‚Рµ СЂР°СЃРїРѕР»РѕР¶РµРЅРёРµ С„Р°Р№Р»Р°, РїСЂРѕРІРµСЂСЊС‚Рµ РёР·РґР°С‚РµР»СЏ Рё С‚РѕР»СЊРєРѕ РїРѕСЃР»Рµ СЌС‚РѕРіРѕ СЂРµС€Р°Р№С‚Рµ, Р·Р°РІРµСЂС€Р°С‚СЊ РїСЂРѕС†РµСЃСЃ РёР»Рё РЅРµС‚.";
        }

        if (finding.Category.Contains("РћС‡РёСЃС‚РєР°", StringComparison.OrdinalIgnoreCase))
        {
            return "РћС‡РёСЃС‚РёС‚Рµ РІСЂРµРјРµРЅРЅС‹Рµ С„Р°Р№Р»С‹ С‡РµСЂРµР· РџР°СЂР°РјРµС‚СЂС‹ Windows РёР»Рё РћС‡РёСЃС‚РєСѓ РґРёСЃРєР°. Р Р°Р±РѕС‡РёРµ РґРѕРєСѓРјРµРЅС‚С‹ Рё РїСЂРѕРµРєС‚С‹ РЅРµ СѓРґР°Р»СЏР№С‚Рµ Р±РµР· РїСЂРѕРІРµСЂРєРё.";
        }

        return "РџСЂРѕРІРµСЂСЊС‚Рµ РїСЂРѕРёСЃС…РѕР¶РґРµРЅРёРµ С„Р°Р№Р»Р°. Р•СЃР»Рё СЌС‚Рѕ С‡Р°СЃС‚СЊ Р·РЅР°РєРѕРјРѕР№ РїСЂРѕРіСЂР°РјРјС‹, РµРіРѕ РјРѕР¶РЅРѕ РѕСЃС‚Р°РІРёС‚СЊ. Р•СЃР»Рё РёСЃС‚РѕС‡РЅРёРє РЅРµСЏСЃРµРЅ, РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕ РїСЂРѕРІРµСЂСЊС‚Рµ С„Р°Р№Р» Р°РЅС‚РёРІРёСЂСѓСЃРѕРј.";
    }

    private void RefreshRecommendations(ScanSummary? summary)
    {
        _recommendationsList.Items.Clear();
        if (summary is null)
        {
            _recommendationsList.Items.Add("Р—Р°РїСѓСЃС‚РёС‚Рµ Р°РЅР°Р»РёР·, С‡С‚РѕР±С‹ РїРѕР»СѓС‡РёС‚СЊ РїРµСЂСЃРѕРЅР°Р»СЊРЅС‹Рµ СЂРµРєРѕРјРµРЅРґР°С†РёРё.");
            _recommendationsList.Items.Add("РџРѕСЃР»Рµ РїСЂРѕРІРµСЂРєРё Р·РґРµСЃСЊ РїРѕСЏРІСЏС‚СЃСЏ С‚РѕР»СЊРєРѕ РєРѕСЂРѕС‚РєРёРµ РїРѕРЅСЏС‚РЅС‹Рµ РґРµР№СЃС‚РІРёСЏ.");
            return;
        }

        if (summary.HighCount > 0)
        {
            _recommendationsList.Items.Add($"РџСЂРѕРІРµСЂСЊС‚Рµ РІС‹СЃРѕРєРёР№ СЂРёСЃРє: {summary.HighCount}. РќР°С‡РЅРёС‚Рµ СЃ Р·Р°С‰РёС‚С‹ Windows Рё Р°РІС‚РѕР·Р°РіСЂСѓР·РєРё.");
        }
        if (summary.MediumCount > 0)
        {
            _recommendationsList.Items.Add($"РџСЂРѕСЃРјРѕС‚СЂРёС‚Рµ СЃСЂРµРґРЅРёРµ СЂРёСЃРєРё: {summary.MediumCount}. Р§Р°С‰Рµ РІСЃРµРіРѕ СЌС‚Рѕ РїСЂРѕС†РµСЃСЃС‹ Рё С„Р°Р№Р»С‹ РёР· РїРѕР»СЊР·РѕРІР°С‚РµР»СЊСЃРєРёС… РїР°РїРѕРє.");
        }
        if (summary.TempBytes > 2L * 1024 * 1024 * 1024)
        {
            _recommendationsList.Items.Add($"РћС‡РёСЃС‚РёС‚Рµ РІСЂРµРјРµРЅРЅС‹Рµ С„Р°Р№Р»С‹: РЅР°Р№РґРµРЅРѕ РїСЂРёРјРµСЂРЅРѕ {SecurityScanner.FormatBytes(summary.TempBytes)}.");
        }
        if (summary.Findings.Any(f => f.Category.Contains("РђРІС‚РѕР·Р°РіСЂСѓР·РєР°", StringComparison.OrdinalIgnoreCase)))
        {
            _recommendationsList.Items.Add("РџСЂРѕРІРµСЂСЊС‚Рµ СЌР»РµРјРµРЅС‚С‹ Р°РІС‚РѕР·Р°РіСЂСѓР·РєРё Рё РѕСЃС‚Р°РІСЊС‚Рµ С‚РѕР»СЊРєРѕ С‚Рµ, РєРѕС‚РѕСЂС‹Рµ РІС‹ С‚РѕС‡РЅРѕ СѓР·РЅР°РµС‚Рµ.");
        }
        if (summary.HighCount == 0 && summary.MediumCount == 0)
        {
            _recommendationsList.Items.Add("РљСЂРёС‚РёС‡РЅС‹С… РґРµР№СЃС‚РІРёР№ РЅРµ С‚СЂРµР±СѓРµС‚СЃСЏ. РџРѕ С‚РµРєСѓС‰РёРј РїСЂР°РІРёР»Р°Рј СЃРёСЃС‚РµРјР° РІС‹РіР»СЏРґРёС‚ СЃРїРѕРєРѕР№РЅРѕ.");
        }
        _recommendationsList.Items.Add("РџРµСЂРµРґ СѓРґР°Р»РµРЅРёРµРј С„Р°Р№Р»РѕРІ РІСЃРµРіРґР° РїСЂРѕРІРµСЂСЏР№С‚Рµ РёСЃС‚РѕС‡РЅРёРє. PC Guardian РїРѕРєР°Р·С‹РІР°РµС‚ СЂРёСЃРє, РЅРѕ РЅРµ СѓРґР°Р»СЏРµС‚ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё.");
    }

    private void SaveHistory(ScanSummary summary)
    {
        var previous = _history.OrderByDescending(h => h.FinishedAt).FirstOrDefault();
        var total = summary.HighCount + summary.MediumCount + summary.LowCount;
        var change = previous is null
            ? "РџРµСЂРІР°СЏ РїСЂРѕРІРµСЂРєР°"
            : total < previous.TotalRisks
                ? $"РЎС‚Р°Р»Рѕ Р»СѓС‡С€Рµ: -{previous.TotalRisks - total}"
                : total > previous.TotalRisks
                    ? $"РЎС‚Р°Р»Рѕ С…СѓР¶Рµ: +{total - previous.TotalRisks}"
                    : "Р‘РµР· РёР·РјРµРЅРµРЅРёР№";

        _history.Insert(0, new ScanHistoryEntry(summary.FinishedAt, summary.HighCount, summary.MediumCount, summary.LowCount, summary.FilesChecked, total, change));
        _history = _history.Take(40).ToList();
        var historyPath = GetHistoryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(historyPath)!);
        File.WriteAllText(historyPath, JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true }));
    }

    private List<ScanHistoryEntry> LoadHistory()
    {
        try
        {
            var historyPath = GetHistoryPath();
            if (!File.Exists(historyPath))
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<ScanHistoryEntry>>(File.ReadAllText(historyPath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void RefreshHistoryGrid()
    {
        if (_historyGrid.Columns.Count == 0)
        {
            return;
        }

        _historyGrid.Rows.Clear();
        foreach (var item in _history.OrderByDescending(h => h.FinishedAt))
        {
            _historyGrid.Rows.Add(item.FinishedAt.ToString("dd.MM.yyyy HH:mm"), item.High, item.Medium, item.Low, item.FilesChecked, item.Change);
        }
    }

    private static string GetHistoryPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC Guardian", "scan-history.json");
    }

    private sealed record ScanHistoryEntry(DateTime FinishedAt, int High, int Medium, int Low, int FilesChecked, int TotalRisks, string Change);
    private void ResetMetricTiles()
    {
        _highCard.Value = "0";
        _mediumCard.Value = "0";
        _lowCard.Value = "0";
        _filesCard.Value = "0";
        _summaryLabel.Text = "Р РёСЃРєРё: 0";
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
            Title = "РЎРѕС…СЂР°РЅРёС‚СЊ РѕС‚С‡РµС‚",
            Filter = "РўРµРєСЃС‚РѕРІС‹Р№ РѕС‚С‡РµС‚ (*.txt)|*.txt",
            FileName = $"pc-guardian-report-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(dialog.FileName, SecurityScanner.BuildReport(_lastSummary));
            AddLog($"РћС‚С‡РµС‚ СЃРѕС…СЂР°РЅРµРЅ: {dialog.FileName}");
        }
    }

    private void ApplyTheme(bool dark)
    {
        _darkTheme = dark;
        var background = dark ? Color.FromArgb(18, 24, 35) : Background;
        var surface = dark ? Color.FromArgb(25, 33, 47) : Surface;
        var surfaceSoft = dark ? Color.FromArgb(22, 30, 44) : SurfaceSoft;
        var ink = dark ? Color.FromArgb(224, 231, 239) : Ink;
        var muted = dark ? Color.FromArgb(164, 176, 194) : Muted;

        BackColor = background;
        if (_mainLayout is not null)
        {
            _mainLayout.BackColor = background;
        }

        foreach (var control in GetAllControls(this))
        {
            if (control is RoundedPanel or Panel or TableLayoutPanel or FlowLayoutPanel)
            {
                if (control.BackColor == Background || control.BackColor == Surface || control.BackColor == SurfaceSoft || control.BackColor == Color.FromArgb(11, 18, 32) || control.BackColor == Color.FromArgb(18, 28, 46) || control.BackColor == Color.FromArgb(24, 36, 58))
                {
                    control.BackColor = control.BackColor == SurfaceSoft || control.BackColor == Color.FromArgb(24, 36, 58) || control.BackColor == Color.FromArgb(31, 40, 56) ? surfaceSoft : surface;
                }
            }

            if (control is Label or CheckBox or ListBox)
            {
                if (control.ForeColor == Ink || control.ForeColor == Color.FromArgb(232, 240, 248))
                {
                    control.ForeColor = ink;
                }
                else if (control.ForeColor == Muted || control.ForeColor == Color.FromArgb(157, 171, 190))
                {
                    control.ForeColor = muted;
                }
            }
        }

        _recommendationsOnlyCheck.ForeColor = ink;
        _darkThemeCheck.ForeColor = ink;
        _findingsGrid.BackgroundColor = surface;
        _findingsGrid.DefaultCellStyle.BackColor = surface;
        _findingsGrid.DefaultCellStyle.ForeColor = ink;
        _findingsGrid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(42, 68, 92) : Color.FromArgb(216, 232, 244);
        _findingsGrid.DefaultCellStyle.SelectionForeColor = ink;
        _findingsGrid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(36, 47, 64) : Color.FromArgb(228, 235, 243);
        _findingsGrid.ColumnHeadersDefaultCellStyle.ForeColor = muted;
        _findingsGrid.GridColor = dark ? Color.FromArgb(45, 60, 82) : Color.FromArgb(226, 232, 240);

        _historyGrid.BackgroundColor = surface;
        _historyGrid.DefaultCellStyle.BackColor = surface;
        _historyGrid.DefaultCellStyle.ForeColor = ink;
        _historyGrid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(42, 68, 92) : Color.FromArgb(216, 232, 244);
        _historyGrid.DefaultCellStyle.SelectionForeColor = ink;
        _historyGrid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(36, 47, 64) : Color.FromArgb(228, 235, 243);
        _historyGrid.ColumnHeadersDefaultCellStyle.ForeColor = muted;
        _historyGrid.GridColor = dark ? Color.FromArgb(45, 60, 82) : Color.FromArgb(226, 232, 240);

        _logList.BackColor = surfaceSoft;
        _logList.ForeColor = ink;
        _recommendationsList.BackColor = surfaceSoft;
        _recommendationsList.ForeColor = ink;
        _detailTitle.ForeColor = ink;
        _detailLocation.ForeColor = dark ? Color.FromArgb(176, 188, 205) : muted;
        _detailWhy.ForeColor = dark ? Color.FromArgb(214, 222, 232) : ink;
        _detailDanger.ForeColor = dark ? Color.FromArgb(214, 222, 232) : ink;
        _detailAction.ForeColor = dark ? Color.FromArgb(214, 222, 232) : ink;
        _detailIgnore.ForeColor = dark ? Color.FromArgb(214, 222, 232) : ink;
        foreach (var detailPanel in _detailPanels)
        {
            detailPanel.BackColor = surfaceSoft;
        }
        foreach (var accentLabel in _detailAccentLabels)
        {
            accentLabel.ForeColor = dark ? Color.FromArgb(74, 191, 201) : Cyan;
        }
        _detailRisk.ForeColor = dark ? Color.FromArgb(116, 176, 243) : Blue;
    }

    private static IEnumerable<Control> GetAllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            foreach (var nested in GetAllControls(child))
            {
                yield return nested;
            }
            yield return child;
        }
    }
    private void UpdateResponsiveLayout()
    {
        _shellLayout?.SuspendLayout();
        _mainLayout?.SuspendLayout();
        _headerLayout?.SuspendLayout();
        _findingsContentLayout?.SuspendLayout();

        if (_actionsLayout is not null)
        {
            var actionsWidth = Math.Max(320, _actionsLayout.ClientSize.Width);
            _actionsLayout.WrapContents = actionsWidth < 900;
            _actionsLayout.AutoScroll = false;
        }

        if (_metricsLayout is not null)
        {
            var available = Math.Max(320, _metricsLayout.ClientSize.Width);
            _metricsLayout.WrapContents = available < 760;
            var columns = available < 760 ? 2 : 4;
            var cardWidth = Math.Max(150, Math.Min(220, (available - (columns - 1) * 12) / columns));
            foreach (var card in new[] { _highCard, _mediumCard, _lowCard, _filesCard })
            {
                card.Width = cardWidth;
                card.Height = 82;
            }
        }

        if (_headerLayout is not null && _headerLayout.ColumnStyles.Count > 1)
        {
            var healthWidth = _headerLayout.ClientSize.Width < 980 ? 250 : 320;
            _headerLayout.ColumnStyles[1].Width = healthWidth;
        }

        if (_findingsContentLayout is not null && _riskDetailsCard is not null)
        {
            var available = _findingsContentLayout.ClientSize.Width;
            if (available <= 980)
            {
                if (_findingsContentLayout.ColumnCount != 1)
                {
                    _findingsContentLayout.Controls.Clear();
                    _findingsContentLayout.ColumnStyles.Clear();
                    _findingsContentLayout.RowStyles.Clear();
                    _findingsContentLayout.ColumnCount = 1;
                    _findingsContentLayout.RowCount = 2;
                    _findingsContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    _findingsContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    _findingsContentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
                    _riskDetailsCard.Margin = new Padding(0, 14, 0, 0);
                    _findingsContentLayout.Controls.Add(_findingsGrid, 0, 0);
                    _findingsContentLayout.Controls.Add(_riskDetailsCard, 0, 1);
                }
            }
            else
            {
                if (_findingsContentLayout.ColumnCount != 2)
                {
                    _findingsContentLayout.Controls.Clear();
                    _findingsContentLayout.ColumnStyles.Clear();
                    _findingsContentLayout.RowStyles.Clear();
                    _findingsContentLayout.ColumnCount = 2;
                    _findingsContentLayout.RowCount = 1;
                    _findingsContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    _findingsContentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 338));
                    _findingsContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    _riskDetailsCard.Margin = new Padding(14, 0, 0, 0);
                    _findingsContentLayout.Controls.Add(_findingsGrid, 0, 0);
                    _findingsContentLayout.Controls.Add(_riskDetailsCard, 1, 0);
                }
            }
        }

        _findingsContentLayout?.ResumeLayout(true);
        _findingsContentLayout?.PerformLayout();
        _headerLayout?.ResumeLayout(true);
        _mainLayout?.ResumeLayout(true);
        _shellLayout?.ResumeLayout(true);
        PerformLayout();
        Invalidate(true);
    }
    private static string ToRussianRisk(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.High => "Р’С‹СЃРѕРєРёР№",
            RiskLevel.Medium => "РЎСЂРµРґРЅРёР№",
            RiskLevel.Low => "РќРёР·РєРёР№",
            _ => "РРЅС„Рѕ"
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
            Width = 160;
            Height = 82;
            BackColor = Surface;
            Radius = 8;
            Margin = new Padding(0, 0, 12, 0);
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
        private Region? _cachedRegion;
        private Size _lastSize;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 8;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = CreatePath();
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
            using var pen = new Pen(BackColor);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cachedRegion?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateRegion()
        {
            if (!IsHandleCreated || ClientSize.Width <= 1 || ClientSize.Height <= 1)
            {
                return;
            }

            if (_lastSize == ClientSize && _cachedRegion is not null)
            {
                return;
            }

            _cachedRegion?.Dispose();
            using var path = CreatePath();
            _cachedRegion = new Region(path);
            Region = _cachedRegion.Clone();
            _lastSize = ClientSize;
            Invalidate();
        }

        private System.Drawing.Drawing2D.GraphicsPath CreatePath()
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            var radius = Math.Min(Radius, Math.Max(1, Math.Min(rect.Width, rect.Height) / 2));
            var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}























