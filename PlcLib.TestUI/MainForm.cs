using System.Text.Json;
using PlcLib;
using PlcLib.Abstractions;
using PlcLib.Common;
using PlcLib.Factories;
using PlcLib.Options;
using PlcLib.Runtime;

namespace PlcLib.TestUI;

public sealed class MainForm : Form
{
    // ── 상태 ──────────────────────────────────────────────────
    private IPlcClient? _client;
    private PlcPollSvc? _pollSvc;
    private readonly List<PlcItemOpt> _items = new();
    private readonly object _logLock = new();

    // ── 공통 연결 컨트롤 ──────────────────────────────────────
    private ComboBox      _cboProvider  = null!;
    private TextBox       _txtName      = null!;
    private NumericUpDown _numPollMs    = null!;
    private NumericUpDown _numReconnMs  = null!;
    private TextBox       _txtHbKey     = null!;
    private NumericUpDown _numHbMs      = null!;
    private Button        _btnConnect   = null!;
    private Button        _btnDisconnect= null!;
    private Button        _btnStartPoll = null!;
    private Button        _btnStopPoll  = null!;

    // ── McpX 패널 ─────────────────────────────────────────────
    private FlowLayoutPanel _panelMcpX    = null!;
    private TextBox         _txtMcpIp     = null!;
    private NumericUpDown   _numMcpPort   = null!;
    private TextBox         _txtMcpPwd    = null!;
    private ComboBox        _cboMcpFrame  = null!;
    private CheckBox        _chkMcpAscii  = null!;
    private CheckBox        _chkMcpUdp    = null!;
    private NumericUpDown   _numMcpTimeout= null!;

    // ── S7 패널 ───────────────────────────────────────────────
    private FlowLayoutPanel _panelS7       = null!;
    private ComboBox        _cboS7Cpu      = null!;
    private TextBox         _txtS7Ip       = null!;
    private NumericUpDown   _numS7Rack     = null!;
    private NumericUpDown   _numS7Slot     = null!;
    private NumericUpDown   _numS7Timeout  = null!;

    // ── Modbus 패널 ───────────────────────────────────────────
    private FlowLayoutPanel _panelModbus       = null!;
    private ComboBox        _cboModbusMode     = null!;
    private TextBox         _txtModbusIp       = null!;
    private NumericUpDown   _numModbusPort     = null!;
    private NumericUpDown   _numModbusSlaveId  = null!;
    private TextBox         _txtModbusCom      = null!;
    private NumericUpDown   _numModbusBaud     = null!;
    private NumericUpDown   _numModbusTimeout  = null!;

    // ── MxComponent 패널 ──────────────────────────────────────
    private FlowLayoutPanel _panelMxComp    = null!;
    private NumericUpDown   _numMxStation   = null!;
    private TextBox         _txtMxPwd       = null!;

    // ── 상태바 ────────────────────────────────────────────────
    private ToolStripStatusLabel _lblConnState = null!;
    private ToolStripStatusLabel _lblPollMs    = null!;
    private ToolStripStatusLabel _lblStats     = null!;

    // ── Items 탭 ──────────────────────────────────────────────
    private DataGridView _gridItems = null!;

    // ── Write 탭 ──────────────────────────────────────────────
    private ComboBox _cboWriteKey   = null!;
    private Label    _lblWriteInfo  = null!;
    private TextBox  _txtWriteValue = null!;
    private Button   _btnWrite      = null!;

    // ── Log 탭 ────────────────────────────────────────────────
    private TextBox _txtLog = null!;

    private System.Windows.Forms.Timer _statsTimer = null!;

    // ═════════════════════════════════════════════════════════
    public MainForm()
    {
        Text          = "PlcLib TestUI";
        MinimumSize   = new Size(900, 680);
        Size          = new Size(1100, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("맑은 고딕", 9f);

        InitializeComponent();
        HookPlcLog();
        UpdateProviderPanel();
        UpdateButtons();
    }

    // ═════════════════════════════════════════════════════════
    private void InitializeComponent()
    {
        SuspendLayout();

        var root = new TableLayoutPanel
        {
            Dock      = DockStyle.Fill,
            RowCount  = 3,
            ColumnCount = 1,
            Padding   = new Padding(4),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // ── 연결 GroupBox ──────────────────────────────────────
        var grpConn = new GroupBox { Text = "연결 설정", Dock = DockStyle.Fill, AutoSize = true };
        root.Controls.Add(grpConn, 0, 0);

        var connInner = new TableLayoutPanel
        {
            Dock       = DockStyle.Fill,
            AutoSize   = true,
            ColumnCount = 1,
            RowCount   = 3,
            Padding    = new Padding(4, 16, 4, 4),
        };
        connInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        connInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        connInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grpConn.Controls.Add(connInner);

        // 공통 설정 행
        var rowCommon = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        connInner.Controls.Add(rowCommon, 0, 0);

        rowCommon.Controls.Add(Lbl("Provider:"));
        _cboProvider = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboProvider.Items.AddRange(new object[] { "Virtual", "McpX", "MxComponent", "S7", "Modbus" });
        _cboProvider.SelectedIndex = 0;
        _cboProvider.SelectedIndexChanged += (_, _) => UpdateProviderPanel();
        rowCommon.Controls.Add(_cboProvider);

        rowCommon.Controls.Add(Lbl("Name:"));
        _txtName = new TextBox { Width = 100, Text = "PLC1" };
        rowCommon.Controls.Add(_txtName);

        rowCommon.Controls.Add(Lbl("PollMs:"));
        _numPollMs = Num(10, 60000, 100);
        rowCommon.Controls.Add(_numPollMs);

        rowCommon.Controls.Add(Lbl("ReconnMs:"));
        _numReconnMs = Num(100, 60000, 1000);
        rowCommon.Controls.Add(_numReconnMs);

        rowCommon.Controls.Add(Lbl("HbKey:"));
        _txtHbKey = new TextBox { Width = 80 };
        rowCommon.Controls.Add(_txtHbKey);

        rowCommon.Controls.Add(Lbl("HbMs:"));
        _numHbMs = Num(0, 300000, 5000);
        rowCommon.Controls.Add(_numHbMs);

        // Provider별 패널 행
        var providerHost = new Panel { AutoSize = true, Dock = DockStyle.Fill, MinimumSize = new Size(0, 28) };
        connInner.Controls.Add(providerHost, 0, 1);

        _panelMcpX  = BuildMcpXPanel();
        _panelS7    = BuildS7Panel();
        _panelModbus = BuildModbusPanel();
        _panelMxComp = BuildMxCompPanel();
        foreach (var p in new Control[] { _panelMcpX, _panelS7, _panelModbus, _panelMxComp })
        {
            p.Visible = false;
            providerHost.Controls.Add(p);
        }

        // 버튼 행
        var rowBtns = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 2, 0, 2) };
        connInner.Controls.Add(rowBtns, 0, 2);

        _btnConnect    = Btn("연결",      Color.FromArgb(0, 120, 215));
        _btnDisconnect = Btn("연결 해제",  Color.FromArgb(196, 43, 28));
        _btnStartPoll  = Btn("폴링 시작",  Color.FromArgb(16, 137, 62));
        _btnStopPoll   = Btn("폴링 중지",  Color.FromArgb(130, 90, 0));

        _btnConnect.Click    += OnConnect;
        _btnDisconnect.Click += OnDisconnect;
        _btnStartPoll.Click  += OnStartPoll;
        _btnStopPoll.Click   += OnStopPoll;

        rowBtns.Controls.Add(_btnConnect);
        rowBtns.Controls.Add(_btnDisconnect);
        rowBtns.Controls.Add(new Panel { Width = 12, Height = 1 });
        rowBtns.Controls.Add(_btnStartPoll);
        rowBtns.Controls.Add(_btnStopPoll);
        rowBtns.Controls.Add(new Panel { Width = 12, Height = 1 });

        var btnLoad = Btn("JSON 로드", SystemColors.Control); btnLoad.ForeColor = SystemColors.ControlText;
        var btnSave = Btn("JSON 저장", SystemColors.Control); btnSave.ForeColor = SystemColors.ControlText;
        btnLoad.Click += OnLoadConfig;
        btnSave.Click += OnSaveConfig;
        rowBtns.Controls.Add(btnLoad);
        rowBtns.Controls.Add(btnSave);

        // ── 상태바 ────────────────────────────────────────────
        var statusStrip = new StatusStrip();
        _lblConnState = new ToolStripStatusLabel("● 미연결")
            { ForeColor = Color.Gray, Font = new Font("맑은 고딕", 9f, FontStyle.Bold) };
        _lblPollMs  = new ToolStripStatusLabel("Poll: -ms");
        _lblStats   = new ToolStripStatusLabel("Block:- Random:- Single:-");
        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _lblConnState,
            new ToolStripSeparator(),
            _lblPollMs,
            new ToolStripSeparator(),
            _lblStats,
        });
        root.Controls.Add(statusStrip, 0, 1);

        // ── TabControl ────────────────────────────────────────
        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Controls.Add(tabs, 0, 2);
        tabs.TabPages.Add(BuildItemsTab());
        tabs.TabPages.Add(BuildWriteTab());
        tabs.TabPages.Add(BuildLogTab());

        // ── 통계 타이머 ───────────────────────────────────────
        _statsTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _statsTimer.Tick += OnStatsTick;
        _statsTimer.Start();

        ResumeLayout(false);
    }

    // ═════════════════════════════════════════════════════════
    // Provider 패널 빌더
    // ═════════════════════════════════════════════════════════

    private FlowLayoutPanel BuildMcpXPanel()
    {
        var p = Flow();
        p.Controls.Add(Lbl("IP:"));
        _txtMcpIp = new TextBox { Width = 130, Text = "192.168.0.1" };
        p.Controls.Add(_txtMcpIp);
        p.Controls.Add(Lbl("Port:"));
        _numMcpPort = Num(1, 65535, 5000);
        p.Controls.Add(_numMcpPort);
        p.Controls.Add(Lbl("Frame:"));
        _cboMcpFrame = new ComboBox { Width = 55, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboMcpFrame.Items.AddRange(new object[] { "E3", "E4", "3E", "4E" });
        _cboMcpFrame.SelectedIndex = 0;
        p.Controls.Add(_cboMcpFrame);
        p.Controls.Add(Lbl("Password:"));
        _txtMcpPwd = new TextBox { Width = 80, PasswordChar = '*' };
        p.Controls.Add(_txtMcpPwd);
        _chkMcpAscii = new CheckBox { Text = "ASCII", AutoSize = true, Margin = new Padding(4, 5, 2, 0) };
        _chkMcpUdp   = new CheckBox { Text = "UDP",   AutoSize = true, Margin = new Padding(2, 5, 2, 0) };
        p.Controls.Add(_chkMcpAscii);
        p.Controls.Add(_chkMcpUdp);
        p.Controls.Add(Lbl("Timeout:"));
        _numMcpTimeout = Num(100, 30000, 5000);
        p.Controls.Add(_numMcpTimeout);
        return p;
    }

    private FlowLayoutPanel BuildS7Panel()
    {
        var p = Flow();
        p.Controls.Add(Lbl("CpuType:"));
        _cboS7Cpu = new ComboBox { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboS7Cpu.Items.AddRange(new object[] { "S71200", "S71500", "S7300", "S7400", "Logo0BA8" });
        _cboS7Cpu.SelectedIndex = 0;
        p.Controls.Add(_cboS7Cpu);
        p.Controls.Add(Lbl("IP:"));
        _txtS7Ip = new TextBox { Width = 130, Text = "192.168.0.1" };
        p.Controls.Add(_txtS7Ip);
        p.Controls.Add(Lbl("Rack:"));
        _numS7Rack = Num(0, 7, 0, 45);
        p.Controls.Add(_numS7Rack);
        p.Controls.Add(Lbl("Slot:"));
        _numS7Slot = Num(0, 15, 1, 45);
        p.Controls.Add(_numS7Slot);
        p.Controls.Add(Lbl("Timeout:"));
        _numS7Timeout = Num(100, 30000, 5000);
        p.Controls.Add(_numS7Timeout);
        return p;
    }

    private FlowLayoutPanel BuildModbusPanel()
    {
        var p = Flow();
        p.Controls.Add(Lbl("Mode:"));
        _cboModbusMode = new ComboBox { Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboModbusMode.Items.AddRange(new object[] { "Tcp", "Rtu" });
        _cboModbusMode.SelectedIndex = 0;
        _cboModbusMode.SelectedIndexChanged += (_, _) =>
        {
            var tcp = _cboModbusMode.SelectedItem?.ToString() == "Tcp";
            _txtModbusIp.Enabled   = tcp;
            _numModbusPort.Enabled = tcp;
            _txtModbusCom.Enabled  = !tcp;
            _numModbusBaud.Enabled = !tcp;
        };
        p.Controls.Add(_cboModbusMode);
        p.Controls.Add(Lbl("IP:"));
        _txtModbusIp = new TextBox { Width = 130, Text = "192.168.0.1" };
        p.Controls.Add(_txtModbusIp);
        p.Controls.Add(Lbl("Port:"));
        _numModbusPort = Num(1, 65535, 502);
        p.Controls.Add(_numModbusPort);
        p.Controls.Add(Lbl("SlaveId:"));
        _numModbusSlaveId = Num(1, 247, 1, 50);
        p.Controls.Add(_numModbusSlaveId);
        p.Controls.Add(Lbl("COM:"));
        _txtModbusCom = new TextBox { Width = 60, Text = "COM1", Enabled = false };
        p.Controls.Add(_txtModbusCom);
        p.Controls.Add(Lbl("Baud:"));
        _numModbusBaud = Num(1200, 921600, 9600, 70); _numModbusBaud.Enabled = false;
        p.Controls.Add(_numModbusBaud);
        p.Controls.Add(Lbl("Timeout:"));
        _numModbusTimeout = Num(100, 30000, 1000);
        p.Controls.Add(_numModbusTimeout);
        return p;
    }

    private FlowLayoutPanel BuildMxCompPanel()
    {
        var p = Flow();
        p.Controls.Add(Lbl("StationNo:"));
        _numMxStation = Num(0, 255, 1);
        p.Controls.Add(_numMxStation);
        p.Controls.Add(Lbl("Password:"));
        _txtMxPwd = new TextBox { Width = 100, PasswordChar = '*' };
        p.Controls.Add(_txtMxPwd);
        return p;
    }

    // ═════════════════════════════════════════════════════════
    // 탭 빌더
    // ═════════════════════════════════════════════════════════

    private TabPage BuildItemsTab()
    {
        var tab = new TabPage("아이템 모니터");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(layout);

        _gridItems = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                { BackColor = Color.FromArgb(245, 245, 250) },
        };

        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colKey",      HeaderText = "Key",    FillWeight = 12 });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colName",     HeaderText = "이름",   FillWeight = 14 });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colAddress",  HeaderText = "주소",   FillWeight = 10 });

        var colType = new DataGridViewComboBoxColumn
            { Name = "colType", HeaderText = "타입", FillWeight = 10, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
        colType.Items.AddRange(Enum.GetNames(typeof(PlcValueType)));
        _gridItems.Columns.Add(colType);

        _gridItems.Columns.Add(new DataGridViewCheckBoxColumn
            { Name = "colWritable",  HeaderText = "쓰기",  FillWeight = 6 });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colValue",     HeaderText = "값",    FillWeight = 14, ReadOnly = true });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colQuality",   HeaderText = "품질",  FillWeight = 8,  ReadOnly = true });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colTimestamp", HeaderText = "갱신",  FillWeight = 16, ReadOnly = true });
        _gridItems.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colDesc",      HeaderText = "설명",  FillWeight = 20 });

        layout.Controls.Add(_gridItems, 0, 0);

        var rowBtns = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(2) };
        var btnApply = new Button { Text = "아이템 적용 (폴링 재시작)", AutoSize = true };
        btnApply.Click += OnApplyItems;
        rowBtns.Controls.Add(btnApply);
        layout.Controls.Add(rowBtns, 0, 1);

        return tab;
    }

    private TabPage BuildWriteTab()
    {
        var tab = new TabPage("값 쓰기");
        var p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 12, 8, 8) };
        tab.Controls.Add(p);

        p.Controls.Add(Lbl("Key:"));
        _cboWriteKey = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboWriteKey.SelectedIndexChanged += OnWriteKeyChanged;
        p.Controls.Add(_cboWriteKey);

        _lblWriteInfo = new Label { AutoSize = true, ForeColor = Color.Gray, Text = "타입: -   주소: -", Margin = new Padding(8, 6, 8, 0) };
        p.Controls.Add(_lblWriteInfo);

        p.Controls.Add(Lbl("값:"));
        _txtWriteValue = new TextBox { Width = 160 };
        p.Controls.Add(_txtWriteValue);

        _btnWrite = new Button { Text = "쓰기", Width = 70 };
        _btnWrite.Click += OnWrite;
        p.Controls.Add(_btnWrite);

        var hint = new Label
        {
            Text = "Bool: true/false 또는 1/0   |   숫자: 그대로 입력   |   String: 텍스트",
            Dock = DockStyle.Top,
            ForeColor = Color.Gray,
            Padding = new Padding(12, 4, 4, 0),
            AutoSize = true,
        };
        tab.Controls.Add(hint);
        return tab;
    }

    private TabPage BuildLogTab()
    {
        var tab = new TabPage("로그");
        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 8.5f),
            BackColor = Color.FromArgb(18, 18, 28),
            ForeColor = Color.FromArgb(200, 210, 220),
            WordWrap = false,
        };
        var btnClear = new Button { Text = "로그 지우기", Dock = DockStyle.Top, Height = 24 };
        btnClear.Click += (_, _) => _txtLog.Clear();
        tab.Controls.Add(_txtLog);
        tab.Controls.Add(btnClear);
        return tab;
    }

    // ═════════════════════════════════════════════════════════
    // 이벤트 핸들러
    // ═════════════════════════════════════════════════════════

    private void OnConnect(object? s, EventArgs e)
    {
        try
        {
            CleanupPoll();
            CleanupClient();
            var opt = BuildClientOpt();
            _client = PlcClientFactory.CreateClient(opt);
            _client.Connect();
            UpdateConnState(true);
            AppendLog(PlcLogLevel.Info, "UI", $"[{opt.Name}] 연결 성공.");
        }
        catch (Exception ex)
        {
            UpdateConnState(false);
            AppendLog(PlcLogLevel.Error, "UI", "연결 실패: " + ex.Message);
            MessageBox.Show("연결 실패:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateButtons();
    }

    private void OnDisconnect(object? s, EventArgs e)
    {
        CleanupPoll();
        CleanupClient();
        UpdateConnState(false);
        AppendLog(PlcLogLevel.Info, "UI", "연결 해제.");
        UpdateButtons();
    }

    private void OnStartPoll(object? s, EventArgs e)
    {
        if (_client == null) { MessageBox.Show("먼저 연결하세요.", "알림"); return; }
        try
        {
            CleanupPoll();
            SyncItemsFromGrid();

            if (_items.Count == 0)
                AppendLog(PlcLogLevel.Warning, "UI", "아이템이 없습니다. 그리드에 아이템을 추가하세요.");

            var pollOpt = new PlcPollOpt
            {
                PollMs   = (int)_numPollMs.Value,
                ReconnMs = (int)_numReconnMs.Value,
                HbKey    = string.IsNullOrWhiteSpace(_txtHbKey.Text) ? null : _txtHbKey.Text.Trim(),
                HbMs     = (int)_numHbMs.Value,
            };
            _pollSvc = new PlcPollSvc(_client, _items, pollOpt);
            _pollSvc.ItemUpdated       += OnItemUpdated;
            _pollSvc.ConnectionChanged += OnConnectionChanged;
            _pollSvc.Start();
            RefreshWriteKeys();
            AppendLog(PlcLogLevel.Info, "UI", $"폴링 시작 (items={_items.Count}, pollMs={pollOpt.PollMs})");
        }
        catch (Exception ex)
        {
            AppendLog(PlcLogLevel.Error, "UI", "폴링 시작 실패: " + ex.Message);
            MessageBox.Show("폴링 시작 실패:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        UpdateButtons();
    }

    private void OnStopPoll(object? s, EventArgs e)
    {
        CleanupPoll();
        AppendLog(PlcLogLevel.Info, "UI", "폴링 중지.");
        UpdateButtons();
    }

    private void OnApplyItems(object? s, EventArgs e)
    {
        if (_pollSvc?.IsRunning != true) return;
        CleanupPoll();
        OnStartPoll(s, e);
    }

    private void OnWrite(object? s, EventArgs e)
    {
        if (_pollSvc == null) { MessageBox.Show("폴링 서비스가 실행 중이어야 합니다.", "알림"); return; }
        var key = _cboWriteKey.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(key)) return;
        if (!_pollSvc.TryGetItem(key, out var item)) return;
        var raw = _txtWriteValue.Text;
        try
        {
            switch (item.Type)
            {
                case PlcValueType.Bool:   _pollSvc.EnqueueWrite(key, raw == "1" || raw.Equals("true",  StringComparison.OrdinalIgnoreCase)); break;
                case PlcValueType.Int16:  _pollSvc.EnqueueWrite(key, short.Parse(raw));  break;
                case PlcValueType.UInt16: _pollSvc.EnqueueWrite(key, ushort.Parse(raw)); break;
                case PlcValueType.Int32:  _pollSvc.EnqueueWrite(key, int.Parse(raw));    break;
                case PlcValueType.UInt32: _pollSvc.EnqueueWrite(key, uint.Parse(raw));   break;
                case PlcValueType.Float:  _pollSvc.EnqueueWrite(key, float.Parse(raw));  break;
                case PlcValueType.String: _pollSvc.EnqueueWrite(key, raw);               break;
            }
            AppendLog(PlcLogLevel.Info, "UI", $"[{key}] 쓰기 큐 등록: {raw}");
        }
        catch (Exception ex)
        {
            MessageBox.Show("쓰기 실패: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnLoadConfig(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON 파일|*.json|모든 파일|*.*",
            Title  = "설정 파일 열기",
            FileName = "plc-settings.json",
        };
        if (File.Exists("plc-settings.json")) dlg.InitialDirectory = Directory.GetCurrentDirectory();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var cfg  = JsonSerializer.Deserialize<PlcSettings>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg != null) ApplySettings(cfg);
            AppendLog(PlcLogLevel.Info, "UI", "설정 로드: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("로드 실패:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveConfig(object? s, EventArgs e)
    {
        using var dlg = new SaveFileDialog { Filter = "JSON 파일|*.json", FileName = "plc-settings.json" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            SyncItemsFromGrid();
            var json = JsonSerializer.Serialize(ExtractSettings(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            AppendLog(PlcLogLevel.Info, "UI", "설정 저장: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show("저장 실패:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnItemUpdated(object? sender, PlcItemSnap snap)
    {
        if (InvokeRequired) { BeginInvoke(() => OnItemUpdated(sender, snap)); return; }
        foreach (DataGridViewRow row in _gridItems.Rows)
        {
            if (row.IsNewRow) continue;
            if (!string.Equals(row.Cells["colKey"].Value?.ToString(), snap.Key, StringComparison.OrdinalIgnoreCase)) continue;
            row.Cells["colValue"].Value     = snap.Value?.ToString() ?? "(null)";
            row.Cells["colQuality"].Value   = snap.IsGood ? "Good" : "Bad";
            row.Cells["colTimestamp"].Value = snap.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");
            row.Cells["colQuality"].Style.ForeColor = snap.IsGood ? Color.FromArgb(0, 140, 0) : Color.Red;
            break;
        }
    }

    private void OnConnectionChanged(object? sender, ConnArgs e)
    {
        if (InvokeRequired) { BeginInvoke(() => OnConnectionChanged(sender, e)); return; }
        UpdateConnState(e.IsConnected);
    }

    private void OnStatsTick(object? sender, EventArgs e)
    {
        if (_pollSvc == null) return;
        _lblPollMs.Text = $"Poll: {_pollSvc.LastPollMs}ms";
        _lblStats.Text  = $"Block:{_pollSvc.LastBlockCount}  Random:{_pollSvc.LastRandomCount}  Single:{_pollSvc.LastSingleCount}";
    }

    private void OnWriteKeyChanged(object? sender, EventArgs e)
    {
        var key = _cboWriteKey.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(key) || _pollSvc == null) { _lblWriteInfo.Text = "타입: -   주소: -"; return; }
        if (_pollSvc.TryGetItem(key, out var item))
            _lblWriteInfo.Text = $"타입: {item.Type}   주소: {item.Address}   쓰기허용: {item.Writable}";
    }

    // ═════════════════════════════════════════════════════════
    // 헬퍼 메서드
    // ═════════════════════════════════════════════════════════

    private void UpdateProviderPanel()
    {
        var p = _cboProvider.SelectedItem?.ToString() ?? "Virtual";
        _panelMcpX.Visible   = p == "McpX";
        _panelS7.Visible     = p == "S7";
        _panelModbus.Visible = p == "Modbus";
        _panelMxComp.Visible = p == "MxComponent";
    }

    private void UpdateButtons()
    {
        var connected = _client?.IsConnected ?? false;
        var polling   = _pollSvc?.IsRunning  ?? false;
        _btnConnect.Enabled    = !connected;
        _btnDisconnect.Enabled =  connected;
        _btnStartPoll.Enabled  =  connected && !polling;
        _btnStopPoll.Enabled   =  polling;
    }

    private void UpdateConnState(bool connected)
    {
        _lblConnState.Text      = connected ? "● 연결됨" : "● 미연결";
        _lblConnState.ForeColor = connected ? Color.FromArgb(16, 137, 62) : Color.Gray;
        UpdateButtons();
    }

    private void RefreshWriteKeys()
    {
        _cboWriteKey.Items.Clear();
        foreach (var item in _items.Where(x => x.Writable))
            _cboWriteKey.Items.Add(item.Key);
        if (_cboWriteKey.Items.Count > 0) _cboWriteKey.SelectedIndex = 0;
    }

    private PlcClientOpt BuildClientOpt()
    {
        var provider = _cboProvider.SelectedItem?.ToString() ?? "Virtual";
        var opt = new PlcClientOpt
        {
            Name     = _txtName.Text.Trim() is { Length: > 0 } n ? n : "PLC",
            Provider = provider,
            Enabled  = true,
            Poll = new PlcPollOpt
            {
                PollMs   = (int)_numPollMs.Value,
                ReconnMs = (int)_numReconnMs.Value,
                HbKey    = string.IsNullOrWhiteSpace(_txtHbKey.Text) ? null : _txtHbKey.Text.Trim(),
                HbMs     = (int)_numHbMs.Value,
            },
        };
        switch (provider)
        {
            case "McpX":
                opt.McpX = new McpXOpt
                {
                    Ip = _txtMcpIp.Text.Trim(), Port = (int)_numMcpPort.Value,
                    Password = _txtMcpPwd.Text, RequestFrame = _cboMcpFrame.SelectedItem?.ToString() ?? "E3",
                    IsAscii = _chkMcpAscii.Checked, IsUdp = _chkMcpUdp.Checked,
                    TimeoutMs = (int)_numMcpTimeout.Value,
                };
                break;
            case "S7":
                opt.S7 = new S7Opt
                {
                    CpuType = _cboS7Cpu.SelectedItem?.ToString() ?? "S71200",
                    Ip = _txtS7Ip.Text.Trim(), Rack = (short)_numS7Rack.Value,
                    Slot = (short)_numS7Slot.Value, TimeoutMs = (int)_numS7Timeout.Value,
                };
                break;
            case "Modbus":
                opt.Modbus = new ModbusOpt
                {
                    Mode = _cboModbusMode.SelectedItem?.ToString() ?? "Tcp",
                    Ip = _txtModbusIp.Text.Trim(), Port = (int)_numModbusPort.Value,
                    SlaveId = (byte)_numModbusSlaveId.Value, PortName = _txtModbusCom.Text.Trim(),
                    BaudRate = (int)_numModbusBaud.Value, TimeoutMs = (int)_numModbusTimeout.Value,
                };
                break;
            case "MxComponent":
                opt.MxComponent = new MxCompOpt
                    { LogicalStationNo = (int)_numMxStation.Value, Password = _txtMxPwd.Text };
                break;
        }
        return opt;
    }

    private void SyncItemsFromGrid()
    {
        _items.Clear();
        var no = 1;
        foreach (DataGridViewRow row in _gridItems.Rows)
        {
            if (row.IsNewRow) continue;
            var key  = row.Cells["colKey"].Value?.ToString();
            var addr = row.Cells["colAddress"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(addr)) continue;
            Enum.TryParse<PlcValueType>(row.Cells["colType"].Value?.ToString(), out var vt);
            _items.Add(new PlcItemOpt
            {
                No          = no++,
                Key         = key,
                Name        = row.Cells["colName"].Value?.ToString() ?? key,
                Address     = addr,
                Type        = vt,
                Writable    = row.Cells["colWritable"].Value is true,
                Description = row.Cells["colDesc"].Value?.ToString(),
            });
        }
    }

    private void ApplySettings(PlcSettings cfg)
    {
        var c = cfg.PlcClient;
        if (c != null)
        {
            _txtName.Text = c.Name ?? "PLC";
            var idx = _cboProvider.Items.IndexOf(c.Provider ?? "Virtual");
            _cboProvider.SelectedIndex = idx >= 0 ? idx : 0;

            if (c.Poll != null)
            {
                _numPollMs.Value   = Clamp(c.Poll.PollMs,   10, 60000);
                _numReconnMs.Value = Clamp(c.Poll.ReconnMs, 100, 60000);
                _txtHbKey.Text     = c.Poll.HbKey ?? "";
                _numHbMs.Value     = Clamp(c.Poll.HbMs, 0, 300000);
            }
            if (c.McpX != null)
            {
                _txtMcpIp.Text = c.McpX.Ip; _numMcpPort.Value = Clamp(c.McpX.Port, 1, 65535);
                _txtMcpPwd.Text = c.McpX.Password; _chkMcpAscii.Checked = c.McpX.IsAscii;
                _chkMcpUdp.Checked = c.McpX.IsUdp; _numMcpTimeout.Value = Clamp(c.McpX.TimeoutMs, 100, 30000);
                var fi = _cboMcpFrame.Items.IndexOf(c.McpX.RequestFrame);
                if (fi >= 0) _cboMcpFrame.SelectedIndex = fi;
            }
            if (c.S7 != null)
            {
                var si = _cboS7Cpu.Items.IndexOf(c.S7.CpuType);
                if (si >= 0) _cboS7Cpu.SelectedIndex = si;
                _txtS7Ip.Text = c.S7.Ip; _numS7Rack.Value = Clamp(c.S7.Rack, 0, 7);
                _numS7Slot.Value = Clamp(c.S7.Slot, 0, 15); _numS7Timeout.Value = Clamp(c.S7.TimeoutMs, 100, 30000);
            }
            if (c.Modbus != null)
            {
                var mi = _cboModbusMode.Items.IndexOf(c.Modbus.Mode);
                if (mi >= 0) _cboModbusMode.SelectedIndex = mi;
                _txtModbusIp.Text = c.Modbus.Ip; _numModbusPort.Value = Clamp(c.Modbus.Port, 1, 65535);
                _numModbusSlaveId.Value = Clamp(c.Modbus.SlaveId, 1, 247);
                _txtModbusCom.Text = c.Modbus.PortName; _numModbusBaud.Value = Clamp(c.Modbus.BaudRate, 1200, 921600);
                _numModbusTimeout.Value = Clamp(c.Modbus.TimeoutMs, 100, 30000);
            }
            if (c.MxComponent != null)
            {
                _numMxStation.Value = Clamp(c.MxComponent.LogicalStationNo, 0, 255);
                _txtMxPwd.Text = c.MxComponent.Password;
            }
        }

        if (cfg.PlcItems?.Count > 0)
        {
            _gridItems.Rows.Clear();
            foreach (var item in cfg.PlcItems)
                _gridItems.Rows.Add(item.Key, item.Name, item.Address,
                    item.Type.ToString(), item.Writable, "", "", "", item.Description);
        }

        UpdateProviderPanel();
    }

    private PlcSettings ExtractSettings()
    {
        SyncItemsFromGrid();
        return new PlcSettings { PlcClient = BuildClientOpt(), PlcItems = _items.ToList() };
    }

    private void CleanupPoll()
    {
        if (_pollSvc == null) return;
        _pollSvc.ItemUpdated       -= OnItemUpdated;
        _pollSvc.ConnectionChanged -= OnConnectionChanged;
        try { _pollSvc.Stop(); } catch { }
        _pollSvc.Dispose();
        _pollSvc = null;
    }

    private void CleanupClient()
    {
        if (_client == null) return;
        try { _client.Disconnect(); } catch { }
        _client.Dispose();
        _client = null;
    }

    private void HookPlcLog()
    {
        PlcLog.Handler = (level, src, msg, ex) =>
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}][{level,-7}][{src}] {msg}";
            if (ex != null) line += $"\r\n  └ {ex.Message}";
            AppendLog(level, src, line, raw: true);
        };
    }

    private void AppendLog(PlcLogLevel level, string src, string msg, bool raw = false)
    {
        var line = raw ? msg : $"[{DateTime.Now:HH:mm:ss.fff}][{level,-7}][{src}] {msg}";
        if (_txtLog.InvokeRequired) { _txtLog.BeginInvoke(() => AppendLog(level, src, line, true)); return; }
        _txtLog.AppendText(line + "\r\n");
        if (_txtLog.Lines.Length > 5000)
            _txtLog.Text = string.Join("\r\n", _txtLog.Lines.Skip(2000));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statsTimer.Stop();
        CleanupPoll();
        CleanupClient();
        PlcLog.Handler = null;
        base.OnFormClosing(e);
    }

    // ── 컨트롤 팩토리 ─────────────────────────────────────────
    private static Label Lbl(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(6, 6, 2, 0) };

    private static NumericUpDown Num(decimal min, decimal max, decimal val, int width = 65) =>
        new() { Minimum = min, Maximum = max, Value = val, Width = width };

    private static Button Btn(string text, Color back) =>
        new() { Text = text, AutoSize = true, BackColor = back, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(2) };

    private static FlowLayoutPanel Flow() =>
        new() { AutoSize = true, WrapContents = false };

    private static decimal Clamp(decimal v, decimal min, decimal max) =>
        v < min ? min : v > max ? max : v;
}

// ── JSON DTO ──────────────────────────────────────────────────
public sealed class PlcSettings
{
    public PlcClientOpt?     PlcClient { get; set; }
    public List<PlcItemOpt>? PlcItems  { get; set; }
}
