namespace PlcLib.TestUI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    // ── 공통 연결 컨트롤 ──────────────────────────────────────
    private ComboBox      _cboProvider   = null!;
    private TextBox       _txtName       = null!;
    private NumericUpDown _numPollMs     = null!;
    private NumericUpDown _numReconnMs   = null!;
    private TextBox       _txtHbKey      = null!;
    private NumericUpDown _numHbMs       = null!;
    private Button        _btnConnect    = null!;
    private Button        _btnDisconnect = null!;
    private Button        _btnStartPoll  = null!;
    private Button        _btnStopPoll   = null!;

    // ── McpX 패널 ─────────────────────────────────────────────
    private FlowLayoutPanel _panelMcpX     = null!;
    private TextBox         _txtMcpIp      = null!;
    private NumericUpDown   _numMcpPort    = null!;
    private TextBox         _txtMcpPwd     = null!;
    private ComboBox        _cboMcpFrame   = null!;
    private CheckBox        _chkMcpAscii   = null!;
    private CheckBox        _chkMcpUdp     = null!;
    private NumericUpDown   _numMcpTimeout = null!;

    // ── S7 패널 ───────────────────────────────────────────────
    private FlowLayoutPanel _panelS7      = null!;
    private ComboBox        _cboS7Cpu     = null!;
    private TextBox         _txtS7Ip      = null!;
    private NumericUpDown   _numS7Rack    = null!;
    private NumericUpDown   _numS7Slot    = null!;
    private NumericUpDown   _numS7Timeout = null!;

    // ── Modbus 패널 ───────────────────────────────────────────
    private FlowLayoutPanel _panelModbus      = null!;
    private ComboBox        _cboModbusMode    = null!;
    private TextBox         _txtModbusIp      = null!;
    private NumericUpDown   _numModbusPort    = null!;
    private NumericUpDown   _numModbusSlaveId = null!;
    private TextBox         _txtModbusCom     = null!;
    private NumericUpDown   _numModbusBaud    = null!;
    private NumericUpDown   _numModbusTimeout = null!;

    // ── MxComponent 패널 ──────────────────────────────────────
    private FlowLayoutPanel _panelMxComp  = null!;
    private NumericUpDown   _numMxStation = null!;
    private TextBox         _txtMxPwd     = null!;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
        root = new TableLayoutPanel();
        grpConn = new GroupBox();
        connInner = new TableLayoutPanel();
        rowCommon = new FlowLayoutPanel();
        lblProvider = new Label();
        _cboProvider = new ComboBox();
        lblName = new Label();
        _txtName = new TextBox();
        lblPollMs = new Label();
        _numPollMs = new NumericUpDown();
        lblReconnMs = new Label();
        _numReconnMs = new NumericUpDown();
        lblHbKey = new Label();
        _txtHbKey = new TextBox();
        lblHbMs = new Label();
        _numHbMs = new NumericUpDown();
        providerHost = new Panel();
        _panelMcpX = new FlowLayoutPanel();
        lblMcpIp = new Label();
        _txtMcpIp = new TextBox();
        lblMcpPort = new Label();
        _numMcpPort = new NumericUpDown();
        lblMcpFrame = new Label();
        _cboMcpFrame = new ComboBox();
        lblMcpPwd = new Label();
        _txtMcpPwd = new TextBox();
        _chkMcpAscii = new CheckBox();
        _chkMcpUdp = new CheckBox();
        lblMcpTimeout = new Label();
        _numMcpTimeout = new NumericUpDown();
        _panelS7 = new FlowLayoutPanel();
        lblS7Cpu = new Label();
        _cboS7Cpu = new ComboBox();
        lblS7Ip = new Label();
        _txtS7Ip = new TextBox();
        lblS7Rack = new Label();
        _numS7Rack = new NumericUpDown();
        lblS7Slot = new Label();
        _numS7Slot = new NumericUpDown();
        lblS7Timeout = new Label();
        _numS7Timeout = new NumericUpDown();
        _panelModbus = new FlowLayoutPanel();
        lblModbusMode = new Label();
        _cboModbusMode = new ComboBox();
        lblModbusIp = new Label();
        _txtModbusIp = new TextBox();
        lblModbusPort = new Label();
        _numModbusPort = new NumericUpDown();
        lblModbusSlaveId = new Label();
        _numModbusSlaveId = new NumericUpDown();
        lblModbusCom = new Label();
        _txtModbusCom = new TextBox();
        lblModbusBaud = new Label();
        _numModbusBaud = new NumericUpDown();
        lblModbusTimeout = new Label();
        _numModbusTimeout = new NumericUpDown();
        _panelMxComp = new FlowLayoutPanel();
        lblMxStation = new Label();
        _numMxStation = new NumericUpDown();
        lblMxPwd = new Label();
        _txtMxPwd = new TextBox();
        rowBtns = new FlowLayoutPanel();
        _btnConnect = new Button();
        _btnDisconnect = new Button();
        spacer1 = new Panel();
        _btnStartPoll = new Button();
        _btnStopPoll = new Button();
        spacer2 = new Panel();
        btnLoad = new Button();
        btnSave = new Button();
        statusStrip = new StatusStrip();
        _lblConnState = new ToolStripStatusLabel();
        _lblPollMs = new ToolStripStatusLabel();
        _lblStats = new ToolStripStatusLabel();
        tabs = new TabControl();
        tabItems = new TabPage();
        tabItemsLayout = new TableLayoutPanel();
        _gridItems = new DataGridView();
        colKey = new DataGridViewTextBoxColumn();
        colName = new DataGridViewTextBoxColumn();
        colAddress = new DataGridViewTextBoxColumn();
        colType = new DataGridViewComboBoxColumn();
        colWritable = new DataGridViewCheckBoxColumn();
        colValue = new DataGridViewTextBoxColumn();
        colQuality = new DataGridViewTextBoxColumn();
        colTimestamp = new DataGridViewTextBoxColumn();
        colDesc = new DataGridViewTextBoxColumn();
        itemsBtnRow = new FlowLayoutPanel();
        btnApply = new Button();
        tabWrite = new TabPage();
        writePanel = new FlowLayoutPanel();
        lblWriteKey = new Label();
        _cboWriteKey = new ComboBox();
        _lblWriteInfo = new Label();
        lblWriteVal = new Label();
        _txtWriteValue = new TextBox();
        _btnWrite = new Button();
        writeHint = new Label();
        tabLog = new TabPage();
        _txtLog = new TextBox();
        btnClearLog = new Button();
        _statsTimer = new System.Windows.Forms.Timer(components);
        root.SuspendLayout();
        grpConn.SuspendLayout();
        connInner.SuspendLayout();
        rowCommon.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numPollMs).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numReconnMs).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numHbMs).BeginInit();
        providerHost.SuspendLayout();
        _panelMcpX.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numMcpPort).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numMcpTimeout).BeginInit();
        _panelS7.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numS7Rack).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numS7Slot).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numS7Timeout).BeginInit();
        _panelModbus.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numModbusPort).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusSlaveId).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusBaud).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusTimeout).BeginInit();
        _panelMxComp.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_numMxStation).BeginInit();
        rowBtns.SuspendLayout();
        statusStrip.SuspendLayout();
        tabs.SuspendLayout();
        tabItems.SuspendLayout();
        tabItemsLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_gridItems).BeginInit();
        itemsBtnRow.SuspendLayout();
        tabWrite.SuspendLayout();
        writePanel.SuspendLayout();
        tabLog.SuspendLayout();
        SuspendLayout();
        // 
        // root
        // 
        root.ColumnCount = 1;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        root.Controls.Add(grpConn, 0, 0);
        root.Controls.Add(statusStrip, 0, 1);
        root.Controls.Add(tabs, 0, 2);
        root.Dock = DockStyle.Fill;
        root.Location = new Point(0, 0);
        root.Name = "root";
        root.Padding = new Padding(4);
        root.RowCount = 3;
        root.RowStyles.Add(new RowStyle());
        root.RowStyles.Add(new RowStyle());
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Size = new Size(1084, 741);
        root.TabIndex = 0;
        // 
        // grpConn
        // 
        grpConn.AutoSize = true;
        grpConn.Controls.Add(connInner);
        grpConn.Dock = DockStyle.Fill;
        grpConn.Location = new Point(7, 7);
        grpConn.Name = "grpConn";
        grpConn.Size = new Size(1070, 156);
        grpConn.TabIndex = 0;
        grpConn.TabStop = false;
        grpConn.Text = "연결 설정";
        // 
        // connInner
        // 
        connInner.AutoSize = true;
        connInner.ColumnCount = 1;
        connInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        connInner.Controls.Add(rowCommon, 0, 0);
        connInner.Controls.Add(providerHost, 0, 1);
        connInner.Controls.Add(rowBtns, 0, 2);
        connInner.Dock = DockStyle.Fill;
        connInner.Location = new Point(3, 19);
        connInner.Name = "connInner";
        connInner.Padding = new Padding(4, 16, 4, 4);
        connInner.RowCount = 3;
        connInner.RowStyles.Add(new RowStyle());
        connInner.RowStyles.Add(new RowStyle());
        connInner.RowStyles.Add(new RowStyle());
        connInner.Size = new Size(1064, 134);
        connInner.TabIndex = 0;
        // 
        // rowCommon
        // 
        rowCommon.AutoSize = true;
        rowCommon.Controls.Add(lblProvider);
        rowCommon.Controls.Add(_cboProvider);
        rowCommon.Controls.Add(lblName);
        rowCommon.Controls.Add(_txtName);
        rowCommon.Controls.Add(lblPollMs);
        rowCommon.Controls.Add(_numPollMs);
        rowCommon.Controls.Add(lblReconnMs);
        rowCommon.Controls.Add(_numReconnMs);
        rowCommon.Controls.Add(lblHbKey);
        rowCommon.Controls.Add(_txtHbKey);
        rowCommon.Controls.Add(lblHbMs);
        rowCommon.Controls.Add(_numHbMs);
        rowCommon.Dock = DockStyle.Fill;
        rowCommon.Location = new Point(7, 19);
        rowCommon.Name = "rowCommon";
        rowCommon.Size = new Size(1050, 29);
        rowCommon.TabIndex = 0;
        // 
        // lblProvider
        // 
        lblProvider.AutoSize = true;
        lblProvider.Location = new Point(6, 6);
        lblProvider.Margin = new Padding(6, 6, 2, 0);
        lblProvider.Name = "lblProvider";
        lblProvider.Size = new Size(54, 15);
        lblProvider.TabIndex = 0;
        lblProvider.Text = "Provider:";
        // 
        // _cboProvider
        // 
        _cboProvider.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboProvider.Items.AddRange(new object[] { "Virtual", "McpX", "MxComponent", "S7", "Modbus" });
        _cboProvider.Location = new Point(65, 3);
        _cboProvider.Name = "_cboProvider";
        _cboProvider.Size = new Size(110, 23);
        _cboProvider.TabIndex = 1;
        _cboProvider.SelectedIndexChanged += OnProviderChanged;
        // 
        // lblName
        // 
        lblName.AutoSize = true;
        lblName.Location = new Point(184, 6);
        lblName.Margin = new Padding(6, 6, 2, 0);
        lblName.Name = "lblName";
        lblName.Size = new Size(42, 15);
        lblName.TabIndex = 2;
        lblName.Text = "Name:";
        // 
        // _txtName
        // 
        _txtName.Location = new Point(231, 3);
        _txtName.Name = "_txtName";
        _txtName.Size = new Size(100, 23);
        _txtName.TabIndex = 3;
        _txtName.Text = "PLC1";
        // 
        // lblPollMs
        // 
        lblPollMs.AutoSize = true;
        lblPollMs.Location = new Point(340, 6);
        lblPollMs.Margin = new Padding(6, 6, 2, 0);
        lblPollMs.Name = "lblPollMs";
        lblPollMs.Size = new Size(46, 15);
        lblPollMs.TabIndex = 4;
        lblPollMs.Text = "PollMs:";
        // 
        // _numPollMs
        // 
        _numPollMs.Location = new Point(391, 3);
        _numPollMs.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
        _numPollMs.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
        _numPollMs.Name = "_numPollMs";
        _numPollMs.Size = new Size(65, 23);
        _numPollMs.TabIndex = 5;
        _numPollMs.Value = new decimal(new int[] { 100, 0, 0, 0 });
        // 
        // lblReconnMs
        // 
        lblReconnMs.AutoSize = true;
        lblReconnMs.Location = new Point(465, 6);
        lblReconnMs.Margin = new Padding(6, 6, 2, 0);
        lblReconnMs.Name = "lblReconnMs";
        lblReconnMs.Size = new Size(66, 15);
        lblReconnMs.TabIndex = 6;
        lblReconnMs.Text = "ReconnMs:";
        // 
        // _numReconnMs
        // 
        _numReconnMs.Location = new Point(536, 3);
        _numReconnMs.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
        _numReconnMs.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
        _numReconnMs.Name = "_numReconnMs";
        _numReconnMs.Size = new Size(65, 23);
        _numReconnMs.TabIndex = 7;
        _numReconnMs.Value = new decimal(new int[] { 1000, 0, 0, 0 });
        // 
        // lblHbKey
        // 
        lblHbKey.AutoSize = true;
        lblHbKey.Location = new Point(610, 6);
        lblHbKey.Margin = new Padding(6, 6, 2, 0);
        lblHbKey.Name = "lblHbKey";
        lblHbKey.Size = new Size(45, 15);
        lblHbKey.TabIndex = 8;
        lblHbKey.Text = "HbKey:";
        // 
        // _txtHbKey
        // 
        _txtHbKey.Location = new Point(660, 3);
        _txtHbKey.Name = "_txtHbKey";
        _txtHbKey.Size = new Size(80, 23);
        _txtHbKey.TabIndex = 9;
        // 
        // lblHbMs
        // 
        lblHbMs.AutoSize = true;
        lblHbMs.Location = new Point(749, 6);
        lblHbMs.Margin = new Padding(6, 6, 2, 0);
        lblHbMs.Name = "lblHbMs";
        lblHbMs.Size = new Size(42, 15);
        lblHbMs.TabIndex = 10;
        lblHbMs.Text = "HbMs:";
        // 
        // _numHbMs
        // 
        _numHbMs.Location = new Point(796, 3);
        _numHbMs.Maximum = new decimal(new int[] { 300000, 0, 0, 0 });
        _numHbMs.Name = "_numHbMs";
        _numHbMs.Size = new Size(65, 23);
        _numHbMs.TabIndex = 11;
        _numHbMs.Value = new decimal(new int[] { 5000, 0, 0, 0 });
        // 
        // providerHost
        // 
        providerHost.AutoSize = true;
        providerHost.Controls.Add(_panelMcpX);
        providerHost.Controls.Add(_panelS7);
        providerHost.Controls.Add(_panelModbus);
        providerHost.Controls.Add(_panelMxComp);
        providerHost.Dock = DockStyle.Fill;
        providerHost.Location = new Point(7, 54);
        providerHost.MinimumSize = new Size(0, 28);
        providerHost.Name = "providerHost";
        providerHost.Size = new Size(1050, 32);
        providerHost.TabIndex = 1;
        // 
        // _panelMcpX
        // 
        _panelMcpX.AutoSize = true;
        _panelMcpX.Controls.Add(lblMcpIp);
        _panelMcpX.Controls.Add(_txtMcpIp);
        _panelMcpX.Controls.Add(lblMcpPort);
        _panelMcpX.Controls.Add(_numMcpPort);
        _panelMcpX.Controls.Add(lblMcpFrame);
        _panelMcpX.Controls.Add(_cboMcpFrame);
        _panelMcpX.Controls.Add(lblMcpPwd);
        _panelMcpX.Controls.Add(_txtMcpPwd);
        _panelMcpX.Controls.Add(_chkMcpAscii);
        _panelMcpX.Controls.Add(_chkMcpUdp);
        _panelMcpX.Controls.Add(lblMcpTimeout);
        _panelMcpX.Controls.Add(_numMcpTimeout);
        _panelMcpX.Location = new Point(0, 0);
        _panelMcpX.Name = "_panelMcpX";
        _panelMcpX.Size = new Size(789, 29);
        _panelMcpX.TabIndex = 0;
        _panelMcpX.Visible = false;
        _panelMcpX.WrapContents = false;
        // 
        // lblMcpIp
        // 
        lblMcpIp.AutoSize = true;
        lblMcpIp.Location = new Point(6, 6);
        lblMcpIp.Margin = new Padding(6, 6, 2, 0);
        lblMcpIp.Name = "lblMcpIp";
        lblMcpIp.Size = new Size(20, 15);
        lblMcpIp.TabIndex = 0;
        lblMcpIp.Text = "IP:";
        // 
        // _txtMcpIp
        // 
        _txtMcpIp.Location = new Point(31, 3);
        _txtMcpIp.Name = "_txtMcpIp";
        _txtMcpIp.Size = new Size(130, 23);
        _txtMcpIp.TabIndex = 1;
        _txtMcpIp.Text = "192.168.0.1";
        // 
        // lblMcpPort
        // 
        lblMcpPort.AutoSize = true;
        lblMcpPort.Location = new Point(170, 6);
        lblMcpPort.Margin = new Padding(6, 6, 2, 0);
        lblMcpPort.Name = "lblMcpPort";
        lblMcpPort.Size = new Size(32, 15);
        lblMcpPort.TabIndex = 2;
        lblMcpPort.Text = "Port:";
        // 
        // _numMcpPort
        // 
        _numMcpPort.Location = new Point(207, 3);
        _numMcpPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        _numMcpPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numMcpPort.Name = "_numMcpPort";
        _numMcpPort.Size = new Size(65, 23);
        _numMcpPort.TabIndex = 3;
        _numMcpPort.Value = new decimal(new int[] { 5000, 0, 0, 0 });
        // 
        // lblMcpFrame
        // 
        lblMcpFrame.AutoSize = true;
        lblMcpFrame.Location = new Point(281, 6);
        lblMcpFrame.Margin = new Padding(6, 6, 2, 0);
        lblMcpFrame.Name = "lblMcpFrame";
        lblMcpFrame.Size = new Size(43, 15);
        lblMcpFrame.TabIndex = 4;
        lblMcpFrame.Text = "Frame:";
        // 
        // _cboMcpFrame
        // 
        _cboMcpFrame.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboMcpFrame.Items.AddRange(new object[] { "E3", "E4", "3E", "4E" });
        _cboMcpFrame.Location = new Point(329, 3);
        _cboMcpFrame.Name = "_cboMcpFrame";
        _cboMcpFrame.Size = new Size(55, 23);
        _cboMcpFrame.TabIndex = 5;
        // 
        // lblMcpPwd
        // 
        lblMcpPwd.AutoSize = true;
        lblMcpPwd.Location = new Point(393, 6);
        lblMcpPwd.Margin = new Padding(6, 6, 2, 0);
        lblMcpPwd.Name = "lblMcpPwd";
        lblMcpPwd.Size = new Size(60, 15);
        lblMcpPwd.TabIndex = 6;
        lblMcpPwd.Text = "Password:";
        // 
        // _txtMcpPwd
        // 
        _txtMcpPwd.Location = new Point(458, 3);
        _txtMcpPwd.Name = "_txtMcpPwd";
        _txtMcpPwd.PasswordChar = '*';
        _txtMcpPwd.Size = new Size(80, 23);
        _txtMcpPwd.TabIndex = 7;
        // 
        // _chkMcpAscii
        // 
        _chkMcpAscii.AutoSize = true;
        _chkMcpAscii.Location = new Point(545, 5);
        _chkMcpAscii.Margin = new Padding(4, 5, 2, 0);
        _chkMcpAscii.Name = "_chkMcpAscii";
        _chkMcpAscii.Size = new Size(55, 19);
        _chkMcpAscii.TabIndex = 8;
        _chkMcpAscii.Text = "ASCII";
        // 
        // _chkMcpUdp
        // 
        _chkMcpUdp.AutoSize = true;
        _chkMcpUdp.Location = new Point(604, 5);
        _chkMcpUdp.Margin = new Padding(2, 5, 2, 0);
        _chkMcpUdp.Name = "_chkMcpUdp";
        _chkMcpUdp.Size = new Size(50, 19);
        _chkMcpUdp.TabIndex = 9;
        _chkMcpUdp.Text = "UDP";
        // 
        // lblMcpTimeout
        // 
        lblMcpTimeout.AutoSize = true;
        lblMcpTimeout.Location = new Point(662, 6);
        lblMcpTimeout.Margin = new Padding(6, 6, 2, 0);
        lblMcpTimeout.Name = "lblMcpTimeout";
        lblMcpTimeout.Size = new Size(54, 15);
        lblMcpTimeout.TabIndex = 10;
        lblMcpTimeout.Text = "Timeout:";
        // 
        // _numMcpTimeout
        // 
        _numMcpTimeout.Location = new Point(721, 3);
        _numMcpTimeout.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
        _numMcpTimeout.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
        _numMcpTimeout.Name = "_numMcpTimeout";
        _numMcpTimeout.Size = new Size(65, 23);
        _numMcpTimeout.TabIndex = 11;
        _numMcpTimeout.Value = new decimal(new int[] { 5000, 0, 0, 0 });
        // 
        // _panelS7
        // 
        _panelS7.AutoSize = true;
        _panelS7.Controls.Add(lblS7Cpu);
        _panelS7.Controls.Add(_cboS7Cpu);
        _panelS7.Controls.Add(lblS7Ip);
        _panelS7.Controls.Add(_txtS7Ip);
        _panelS7.Controls.Add(lblS7Rack);
        _panelS7.Controls.Add(_numS7Rack);
        _panelS7.Controls.Add(lblS7Slot);
        _panelS7.Controls.Add(_numS7Slot);
        _panelS7.Controls.Add(lblS7Timeout);
        _panelS7.Controls.Add(_numS7Timeout);
        _panelS7.Location = new Point(0, 0);
        _panelS7.Name = "_panelS7";
        _panelS7.Size = new Size(642, 29);
        _panelS7.TabIndex = 1;
        _panelS7.Visible = false;
        _panelS7.WrapContents = false;
        // 
        // lblS7Cpu
        // 
        lblS7Cpu.AutoSize = true;
        lblS7Cpu.Location = new Point(6, 6);
        lblS7Cpu.Margin = new Padding(6, 6, 2, 0);
        lblS7Cpu.Name = "lblS7Cpu";
        lblS7Cpu.Size = new Size(57, 15);
        lblS7Cpu.TabIndex = 0;
        lblS7Cpu.Text = "CpuType:";
        // 
        // _cboS7Cpu
        // 
        _cboS7Cpu.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboS7Cpu.Items.AddRange(new object[] { "S71200", "S71500", "S7300", "S7400", "Logo0BA8" });
        _cboS7Cpu.Location = new Point(68, 3);
        _cboS7Cpu.Name = "_cboS7Cpu";
        _cboS7Cpu.Size = new Size(90, 23);
        _cboS7Cpu.TabIndex = 1;
        // 
        // lblS7Ip
        // 
        lblS7Ip.AutoSize = true;
        lblS7Ip.Location = new Point(167, 6);
        lblS7Ip.Margin = new Padding(6, 6, 2, 0);
        lblS7Ip.Name = "lblS7Ip";
        lblS7Ip.Size = new Size(20, 15);
        lblS7Ip.TabIndex = 2;
        lblS7Ip.Text = "IP:";
        // 
        // _txtS7Ip
        // 
        _txtS7Ip.Location = new Point(192, 3);
        _txtS7Ip.Name = "_txtS7Ip";
        _txtS7Ip.Size = new Size(130, 23);
        _txtS7Ip.TabIndex = 3;
        _txtS7Ip.Text = "192.168.0.1";
        // 
        // lblS7Rack
        // 
        lblS7Rack.AutoSize = true;
        lblS7Rack.Location = new Point(331, 6);
        lblS7Rack.Margin = new Padding(6, 6, 2, 0);
        lblS7Rack.Name = "lblS7Rack";
        lblS7Rack.Size = new Size(35, 15);
        lblS7Rack.TabIndex = 4;
        lblS7Rack.Text = "Rack:";
        // 
        // _numS7Rack
        // 
        _numS7Rack.Location = new Point(371, 3);
        _numS7Rack.Maximum = new decimal(new int[] { 7, 0, 0, 0 });
        _numS7Rack.Name = "_numS7Rack";
        _numS7Rack.Size = new Size(45, 23);
        _numS7Rack.TabIndex = 5;
        // 
        // lblS7Slot
        // 
        lblS7Slot.AutoSize = true;
        lblS7Slot.Location = new Point(425, 6);
        lblS7Slot.Margin = new Padding(6, 6, 2, 0);
        lblS7Slot.Name = "lblS7Slot";
        lblS7Slot.Size = new Size(31, 15);
        lblS7Slot.TabIndex = 6;
        lblS7Slot.Text = "Slot:";
        // 
        // _numS7Slot
        // 
        _numS7Slot.Location = new Point(461, 3);
        _numS7Slot.Maximum = new decimal(new int[] { 15, 0, 0, 0 });
        _numS7Slot.Name = "_numS7Slot";
        _numS7Slot.Size = new Size(45, 23);
        _numS7Slot.TabIndex = 7;
        _numS7Slot.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // lblS7Timeout
        // 
        lblS7Timeout.AutoSize = true;
        lblS7Timeout.Location = new Point(515, 6);
        lblS7Timeout.Margin = new Padding(6, 6, 2, 0);
        lblS7Timeout.Name = "lblS7Timeout";
        lblS7Timeout.Size = new Size(54, 15);
        lblS7Timeout.TabIndex = 8;
        lblS7Timeout.Text = "Timeout:";
        // 
        // _numS7Timeout
        // 
        _numS7Timeout.Location = new Point(574, 3);
        _numS7Timeout.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
        _numS7Timeout.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
        _numS7Timeout.Name = "_numS7Timeout";
        _numS7Timeout.Size = new Size(65, 23);
        _numS7Timeout.TabIndex = 9;
        _numS7Timeout.Value = new decimal(new int[] { 5000, 0, 0, 0 });
        // 
        // _panelModbus
        // 
        _panelModbus.AutoSize = true;
        _panelModbus.Controls.Add(lblModbusMode);
        _panelModbus.Controls.Add(_cboModbusMode);
        _panelModbus.Controls.Add(lblModbusIp);
        _panelModbus.Controls.Add(_txtModbusIp);
        _panelModbus.Controls.Add(lblModbusPort);
        _panelModbus.Controls.Add(_numModbusPort);
        _panelModbus.Controls.Add(lblModbusSlaveId);
        _panelModbus.Controls.Add(_numModbusSlaveId);
        _panelModbus.Controls.Add(lblModbusCom);
        _panelModbus.Controls.Add(_txtModbusCom);
        _panelModbus.Controls.Add(lblModbusBaud);
        _panelModbus.Controls.Add(_numModbusBaud);
        _panelModbus.Controls.Add(lblModbusTimeout);
        _panelModbus.Controls.Add(_numModbusTimeout);
        _panelModbus.Location = new Point(0, 0);
        _panelModbus.Name = "_panelModbus";
        _panelModbus.Size = new Size(868, 29);
        _panelModbus.TabIndex = 2;
        _panelModbus.Visible = false;
        _panelModbus.WrapContents = false;
        // 
        // lblModbusMode
        // 
        lblModbusMode.AutoSize = true;
        lblModbusMode.Location = new Point(6, 6);
        lblModbusMode.Margin = new Padding(6, 6, 2, 0);
        lblModbusMode.Name = "lblModbusMode";
        lblModbusMode.Size = new Size(41, 15);
        lblModbusMode.TabIndex = 0;
        lblModbusMode.Text = "Mode:";
        // 
        // _cboModbusMode
        // 
        _cboModbusMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboModbusMode.Items.AddRange(new object[] { "Tcp", "Rtu" });
        _cboModbusMode.Location = new Point(52, 3);
        _cboModbusMode.Name = "_cboModbusMode";
        _cboModbusMode.Size = new Size(60, 23);
        _cboModbusMode.TabIndex = 1;
        _cboModbusMode.SelectedIndexChanged += OnModbusModeChanged;
        // 
        // lblModbusIp
        // 
        lblModbusIp.AutoSize = true;
        lblModbusIp.Location = new Point(121, 6);
        lblModbusIp.Margin = new Padding(6, 6, 2, 0);
        lblModbusIp.Name = "lblModbusIp";
        lblModbusIp.Size = new Size(20, 15);
        lblModbusIp.TabIndex = 2;
        lblModbusIp.Text = "IP:";
        // 
        // _txtModbusIp
        // 
        _txtModbusIp.Location = new Point(146, 3);
        _txtModbusIp.Name = "_txtModbusIp";
        _txtModbusIp.Size = new Size(130, 23);
        _txtModbusIp.TabIndex = 3;
        _txtModbusIp.Text = "192.168.0.1";
        // 
        // lblModbusPort
        // 
        lblModbusPort.AutoSize = true;
        lblModbusPort.Location = new Point(285, 6);
        lblModbusPort.Margin = new Padding(6, 6, 2, 0);
        lblModbusPort.Name = "lblModbusPort";
        lblModbusPort.Size = new Size(32, 15);
        lblModbusPort.TabIndex = 4;
        lblModbusPort.Text = "Port:";
        // 
        // _numModbusPort
        // 
        _numModbusPort.Location = new Point(322, 3);
        _numModbusPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        _numModbusPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numModbusPort.Name = "_numModbusPort";
        _numModbusPort.Size = new Size(65, 23);
        _numModbusPort.TabIndex = 5;
        _numModbusPort.Value = new decimal(new int[] { 502, 0, 0, 0 });
        // 
        // lblModbusSlaveId
        // 
        lblModbusSlaveId.AutoSize = true;
        lblModbusSlaveId.Location = new Point(396, 6);
        lblModbusSlaveId.Margin = new Padding(6, 6, 2, 0);
        lblModbusSlaveId.Name = "lblModbusSlaveId";
        lblModbusSlaveId.Size = new Size(48, 15);
        lblModbusSlaveId.TabIndex = 6;
        lblModbusSlaveId.Text = "SlaveId:";
        // 
        // _numModbusSlaveId
        // 
        _numModbusSlaveId.Location = new Point(449, 3);
        _numModbusSlaveId.Maximum = new decimal(new int[] { 247, 0, 0, 0 });
        _numModbusSlaveId.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _numModbusSlaveId.Name = "_numModbusSlaveId";
        _numModbusSlaveId.Size = new Size(50, 23);
        _numModbusSlaveId.TabIndex = 7;
        _numModbusSlaveId.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // lblModbusCom
        // 
        lblModbusCom.AutoSize = true;
        lblModbusCom.Location = new Point(508, 6);
        lblModbusCom.Margin = new Padding(6, 6, 2, 0);
        lblModbusCom.Name = "lblModbusCom";
        lblModbusCom.Size = new Size(38, 15);
        lblModbusCom.TabIndex = 8;
        lblModbusCom.Text = "COM:";
        // 
        // _txtModbusCom
        // 
        _txtModbusCom.Enabled = false;
        _txtModbusCom.Location = new Point(551, 3);
        _txtModbusCom.Name = "_txtModbusCom";
        _txtModbusCom.Size = new Size(60, 23);
        _txtModbusCom.TabIndex = 9;
        _txtModbusCom.Text = "COM1";
        // 
        // lblModbusBaud
        // 
        lblModbusBaud.AutoSize = true;
        lblModbusBaud.Location = new Point(620, 6);
        lblModbusBaud.Margin = new Padding(6, 6, 2, 0);
        lblModbusBaud.Name = "lblModbusBaud";
        lblModbusBaud.Size = new Size(37, 15);
        lblModbusBaud.TabIndex = 10;
        lblModbusBaud.Text = "Baud:";
        // 
        // _numModbusBaud
        // 
        _numModbusBaud.Enabled = false;
        _numModbusBaud.Location = new Point(662, 3);
        _numModbusBaud.Maximum = new decimal(new int[] { 921600, 0, 0, 0 });
        _numModbusBaud.Minimum = new decimal(new int[] { 1200, 0, 0, 0 });
        _numModbusBaud.Name = "_numModbusBaud";
        _numModbusBaud.Size = new Size(70, 23);
        _numModbusBaud.TabIndex = 11;
        _numModbusBaud.Value = new decimal(new int[] { 9600, 0, 0, 0 });
        // 
        // lblModbusTimeout
        // 
        lblModbusTimeout.AutoSize = true;
        lblModbusTimeout.Location = new Point(741, 6);
        lblModbusTimeout.Margin = new Padding(6, 6, 2, 0);
        lblModbusTimeout.Name = "lblModbusTimeout";
        lblModbusTimeout.Size = new Size(54, 15);
        lblModbusTimeout.TabIndex = 12;
        lblModbusTimeout.Text = "Timeout:";
        // 
        // _numModbusTimeout
        // 
        _numModbusTimeout.Location = new Point(800, 3);
        _numModbusTimeout.Maximum = new decimal(new int[] { 30000, 0, 0, 0 });
        _numModbusTimeout.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
        _numModbusTimeout.Name = "_numModbusTimeout";
        _numModbusTimeout.Size = new Size(65, 23);
        _numModbusTimeout.TabIndex = 13;
        _numModbusTimeout.Value = new decimal(new int[] { 1000, 0, 0, 0 });
        // 
        // _panelMxComp
        // 
        _panelMxComp.AutoSize = true;
        _panelMxComp.Controls.Add(lblMxStation);
        _panelMxComp.Controls.Add(_numMxStation);
        _panelMxComp.Controls.Add(lblMxPwd);
        _panelMxComp.Controls.Add(_txtMxPwd);
        _panelMxComp.Location = new Point(0, 0);
        _panelMxComp.Name = "_panelMxComp";
        _panelMxComp.Size = new Size(317, 29);
        _panelMxComp.TabIndex = 3;
        _panelMxComp.Visible = false;
        _panelMxComp.WrapContents = false;
        // 
        // lblMxStation
        // 
        lblMxStation.AutoSize = true;
        lblMxStation.Location = new Point(6, 6);
        lblMxStation.Margin = new Padding(6, 6, 2, 0);
        lblMxStation.Name = "lblMxStation";
        lblMxStation.Size = new Size(64, 15);
        lblMxStation.TabIndex = 0;
        lblMxStation.Text = "StationNo:";
        // 
        // _numMxStation
        // 
        _numMxStation.Location = new Point(75, 3);
        _numMxStation.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
        _numMxStation.Name = "_numMxStation";
        _numMxStation.Size = new Size(65, 23);
        _numMxStation.TabIndex = 1;
        _numMxStation.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // lblMxPwd
        // 
        lblMxPwd.AutoSize = true;
        lblMxPwd.Location = new Point(149, 6);
        lblMxPwd.Margin = new Padding(6, 6, 2, 0);
        lblMxPwd.Name = "lblMxPwd";
        lblMxPwd.Size = new Size(60, 15);
        lblMxPwd.TabIndex = 2;
        lblMxPwd.Text = "Password:";
        // 
        // _txtMxPwd
        // 
        _txtMxPwd.Location = new Point(214, 3);
        _txtMxPwd.Name = "_txtMxPwd";
        _txtMxPwd.PasswordChar = '*';
        _txtMxPwd.Size = new Size(100, 23);
        _txtMxPwd.TabIndex = 3;
        // 
        // rowBtns
        // 
        rowBtns.AutoSize = true;
        rowBtns.Controls.Add(_btnConnect);
        rowBtns.Controls.Add(_btnDisconnect);
        rowBtns.Controls.Add(spacer1);
        rowBtns.Controls.Add(_btnStartPoll);
        rowBtns.Controls.Add(_btnStopPoll);
        rowBtns.Controls.Add(spacer2);
        rowBtns.Controls.Add(btnLoad);
        rowBtns.Controls.Add(btnSave);
        rowBtns.Dock = DockStyle.Fill;
        rowBtns.Location = new Point(7, 92);
        rowBtns.Name = "rowBtns";
        rowBtns.Padding = new Padding(0, 2, 0, 2);
        rowBtns.Size = new Size(1050, 35);
        rowBtns.TabIndex = 2;
        // 
        // _btnConnect
        // 
        _btnConnect.AutoSize = true;
        _btnConnect.BackColor = Color.FromArgb(0, 120, 215);
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.ForeColor = Color.White;
        _btnConnect.Location = new Point(2, 4);
        _btnConnect.Margin = new Padding(2);
        _btnConnect.Name = "_btnConnect";
        _btnConnect.Size = new Size(75, 27);
        _btnConnect.TabIndex = 0;
        _btnConnect.Text = "연결";
        _btnConnect.UseVisualStyleBackColor = false;
        _btnConnect.Click += OnConnect;
        // 
        // _btnDisconnect
        // 
        _btnDisconnect.AutoSize = true;
        _btnDisconnect.BackColor = Color.FromArgb(196, 43, 28);
        _btnDisconnect.FlatStyle = FlatStyle.Flat;
        _btnDisconnect.ForeColor = Color.White;
        _btnDisconnect.Location = new Point(81, 4);
        _btnDisconnect.Margin = new Padding(2);
        _btnDisconnect.Name = "_btnDisconnect";
        _btnDisconnect.Size = new Size(75, 27);
        _btnDisconnect.TabIndex = 1;
        _btnDisconnect.Text = "연결 해제";
        _btnDisconnect.UseVisualStyleBackColor = false;
        _btnDisconnect.Click += OnDisconnect;
        // 
        // spacer1
        // 
        spacer1.Location = new Point(161, 5);
        spacer1.Name = "spacer1";
        spacer1.Size = new Size(12, 1);
        spacer1.TabIndex = 2;
        // 
        // _btnStartPoll
        // 
        _btnStartPoll.AutoSize = true;
        _btnStartPoll.BackColor = Color.FromArgb(16, 137, 62);
        _btnStartPoll.FlatStyle = FlatStyle.Flat;
        _btnStartPoll.ForeColor = Color.White;
        _btnStartPoll.Location = new Point(178, 4);
        _btnStartPoll.Margin = new Padding(2);
        _btnStartPoll.Name = "_btnStartPoll";
        _btnStartPoll.Size = new Size(75, 27);
        _btnStartPoll.TabIndex = 3;
        _btnStartPoll.Text = "폴링 시작";
        _btnStartPoll.UseVisualStyleBackColor = false;
        _btnStartPoll.Click += OnStartPoll;
        // 
        // _btnStopPoll
        // 
        _btnStopPoll.AutoSize = true;
        _btnStopPoll.BackColor = Color.FromArgb(130, 90, 0);
        _btnStopPoll.FlatStyle = FlatStyle.Flat;
        _btnStopPoll.ForeColor = Color.White;
        _btnStopPoll.Location = new Point(257, 4);
        _btnStopPoll.Margin = new Padding(2);
        _btnStopPoll.Name = "_btnStopPoll";
        _btnStopPoll.Size = new Size(75, 27);
        _btnStopPoll.TabIndex = 4;
        _btnStopPoll.Text = "폴링 중지";
        _btnStopPoll.UseVisualStyleBackColor = false;
        _btnStopPoll.Click += OnStopPoll;
        // 
        // spacer2
        // 
        spacer2.Location = new Point(337, 5);
        spacer2.Name = "spacer2";
        spacer2.Size = new Size(12, 1);
        spacer2.TabIndex = 5;
        // 
        // btnLoad
        // 
        btnLoad.AutoSize = true;
        btnLoad.BackColor = SystemColors.Control;
        btnLoad.FlatStyle = FlatStyle.Flat;
        btnLoad.ForeColor = SystemColors.ControlText;
        btnLoad.Location = new Point(354, 4);
        btnLoad.Margin = new Padding(2);
        btnLoad.Name = "btnLoad";
        btnLoad.Size = new Size(76, 27);
        btnLoad.TabIndex = 6;
        btnLoad.Text = "JSON 로드";
        btnLoad.UseVisualStyleBackColor = false;
        btnLoad.Click += OnLoadConfig;
        // 
        // btnSave
        // 
        btnSave.AutoSize = true;
        btnSave.BackColor = SystemColors.Control;
        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.ForeColor = SystemColors.ControlText;
        btnSave.Location = new Point(434, 4);
        btnSave.Margin = new Padding(2);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(76, 27);
        btnSave.TabIndex = 7;
        btnSave.Text = "JSON 저장";
        btnSave.UseVisualStyleBackColor = false;
        btnSave.Click += OnSaveConfig;
        // 
        // statusStrip
        // 
        statusStrip.Items.AddRange(new ToolStripItem[] { _lblConnState, _lblPollMs, _lblStats });
        statusStrip.Location = new Point(4, 166);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1076, 22);
        statusStrip.TabIndex = 1;
        // 
        // _lblConnState
        // 
        _lblConnState.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        _lblConnState.ForeColor = Color.Gray;
        _lblConnState.Name = "_lblConnState";
        _lblConnState.Size = new Size(59, 17);
        _lblConnState.Text = "● 미연결";
        // 
        // _lblPollMs
        // 
        _lblPollMs.Name = "_lblPollMs";
        _lblPollMs.Size = new Size(55, 17);
        _lblPollMs.Text = "Poll: -ms";
        // 
        // _lblStats
        // 
        _lblStats.Name = "_lblStats";
        _lblStats.Size = new Size(146, 17);
        _lblStats.Text = "Block:- Random:- Single:-";
        // 
        // tabs
        // 
        tabs.Controls.Add(tabItems);
        tabs.Controls.Add(tabWrite);
        tabs.Controls.Add(tabLog);
        tabs.Dock = DockStyle.Fill;
        tabs.Location = new Point(7, 191);
        tabs.Name = "tabs";
        tabs.SelectedIndex = 0;
        tabs.Size = new Size(1070, 543);
        tabs.TabIndex = 2;
        // 
        // tabItems
        // 
        tabItems.Controls.Add(tabItemsLayout);
        tabItems.Location = new Point(4, 24);
        tabItems.Name = "tabItems";
        tabItems.Size = new Size(1062, 515);
        tabItems.TabIndex = 0;
        tabItems.Text = "아이템 모니터";
        // 
        // tabItemsLayout
        // 
        tabItemsLayout.ColumnCount = 1;
        tabItemsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        tabItemsLayout.Controls.Add(_gridItems, 0, 0);
        tabItemsLayout.Controls.Add(itemsBtnRow, 0, 1);
        tabItemsLayout.Dock = DockStyle.Fill;
        tabItemsLayout.Location = new Point(0, 0);
        tabItemsLayout.Name = "tabItemsLayout";
        tabItemsLayout.RowCount = 2;
        tabItemsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tabItemsLayout.RowStyles.Add(new RowStyle());
        tabItemsLayout.Size = new Size(1062, 515);
        tabItemsLayout.TabIndex = 0;
        // 
        // _gridItems
        // 
        dataGridViewCellStyle1.BackColor = Color.FromArgb(245, 245, 250);
        _gridItems.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
        _gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridItems.BackgroundColor = SystemColors.Window;
        _gridItems.BorderStyle = BorderStyle.None;
        _gridItems.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        _gridItems.Columns.AddRange(new DataGridViewColumn[] { colKey, colName, colAddress, colType, colWritable, colValue, colQuality, colTimestamp, colDesc });
        _gridItems.Dock = DockStyle.Fill;
        _gridItems.Location = new Point(3, 3);
        _gridItems.Name = "_gridItems";
        _gridItems.RowHeadersVisible = false;
        _gridItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridItems.Size = new Size(1056, 468);
        _gridItems.TabIndex = 0;
        // 
        // colKey
        // 
        colKey.FillWeight = 12F;
        colKey.HeaderText = "Key";
        colKey.Name = "colKey";
        // 
        // colName
        // 
        colName.FillWeight = 14F;
        colName.HeaderText = "이름";
        colName.Name = "colName";
        // 
        // colAddress
        // 
        colAddress.FillWeight = 10F;
        colAddress.HeaderText = "주소";
        colAddress.Name = "colAddress";
        // 
        // colType
        // 
        colType.FillWeight = 10F;
        colType.HeaderText = "타입";
        colType.Items.AddRange(new object[] { "Bool", "Int16", "UInt16", "Int32", "UInt32", "Float", "String" });
        colType.Name = "colType";
        // 
        // colWritable
        // 
        colWritable.FillWeight = 6F;
        colWritable.HeaderText = "쓰기";
        colWritable.Name = "colWritable";
        // 
        // colValue
        // 
        colValue.FillWeight = 14F;
        colValue.HeaderText = "값";
        colValue.Name = "colValue";
        colValue.ReadOnly = true;
        // 
        // colQuality
        // 
        colQuality.FillWeight = 8F;
        colQuality.HeaderText = "품질";
        colQuality.Name = "colQuality";
        colQuality.ReadOnly = true;
        // 
        // colTimestamp
        // 
        colTimestamp.FillWeight = 16F;
        colTimestamp.HeaderText = "갱신";
        colTimestamp.Name = "colTimestamp";
        colTimestamp.ReadOnly = true;
        // 
        // colDesc
        // 
        colDesc.FillWeight = 20F;
        colDesc.HeaderText = "설명";
        colDesc.Name = "colDesc";
        // 
        // itemsBtnRow
        // 
        itemsBtnRow.AutoSize = true;
        itemsBtnRow.Controls.Add(btnApply);
        itemsBtnRow.Dock = DockStyle.Fill;
        itemsBtnRow.Location = new Point(3, 477);
        itemsBtnRow.Name = "itemsBtnRow";
        itemsBtnRow.Padding = new Padding(2);
        itemsBtnRow.Size = new Size(1056, 35);
        itemsBtnRow.TabIndex = 1;
        // 
        // btnApply
        // 
        btnApply.AutoSize = true;
        btnApply.Location = new Point(5, 5);
        btnApply.Name = "btnApply";
        btnApply.Size = new Size(157, 25);
        btnApply.TabIndex = 0;
        btnApply.Text = "아이템 적용 (폴링 재시작)";
        btnApply.Click += OnApplyItems;
        // 
        // tabWrite
        // 
        tabWrite.Controls.Add(writePanel);
        tabWrite.Controls.Add(writeHint);
        tabWrite.Location = new Point(4, 24);
        tabWrite.Name = "tabWrite";
        tabWrite.Size = new Size(1062, 479);
        tabWrite.TabIndex = 1;
        tabWrite.Text = "값 쓰기";
        // 
        // writePanel
        // 
        writePanel.AutoSize = true;
        writePanel.Controls.Add(lblWriteKey);
        writePanel.Controls.Add(_cboWriteKey);
        writePanel.Controls.Add(_lblWriteInfo);
        writePanel.Controls.Add(lblWriteVal);
        writePanel.Controls.Add(_txtWriteValue);
        writePanel.Controls.Add(_btnWrite);
        writePanel.Dock = DockStyle.Top;
        writePanel.Location = new Point(0, 19);
        writePanel.Name = "writePanel";
        writePanel.Padding = new Padding(8, 12, 8, 8);
        writePanel.Size = new Size(1062, 49);
        writePanel.TabIndex = 0;
        // 
        // lblWriteKey
        // 
        lblWriteKey.AutoSize = true;
        lblWriteKey.Location = new Point(14, 18);
        lblWriteKey.Margin = new Padding(6, 6, 2, 0);
        lblWriteKey.Name = "lblWriteKey";
        lblWriteKey.Size = new Size(29, 15);
        lblWriteKey.TabIndex = 0;
        lblWriteKey.Text = "Key:";
        // 
        // _cboWriteKey
        // 
        _cboWriteKey.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboWriteKey.Location = new Point(48, 15);
        _cboWriteKey.Name = "_cboWriteKey";
        _cboWriteKey.Size = new Size(160, 23);
        _cboWriteKey.TabIndex = 1;
        _cboWriteKey.SelectedIndexChanged += OnWriteKeyChanged;
        // 
        // _lblWriteInfo
        // 
        _lblWriteInfo.AutoSize = true;
        _lblWriteInfo.ForeColor = Color.Gray;
        _lblWriteInfo.Location = new Point(219, 18);
        _lblWriteInfo.Margin = new Padding(8, 6, 8, 0);
        _lblWriteInfo.Name = "_lblWriteInfo";
        _lblWriteInfo.Size = new Size(91, 15);
        _lblWriteInfo.TabIndex = 2;
        _lblWriteInfo.Text = "타입: -   주소: -";
        // 
        // lblWriteVal
        // 
        lblWriteVal.AutoSize = true;
        lblWriteVal.Location = new Point(324, 18);
        lblWriteVal.Margin = new Padding(6, 6, 2, 0);
        lblWriteVal.Name = "lblWriteVal";
        lblWriteVal.Size = new Size(22, 15);
        lblWriteVal.TabIndex = 3;
        lblWriteVal.Text = "값:";
        // 
        // _txtWriteValue
        // 
        _txtWriteValue.Location = new Point(351, 15);
        _txtWriteValue.Name = "_txtWriteValue";
        _txtWriteValue.Size = new Size(160, 23);
        _txtWriteValue.TabIndex = 4;
        // 
        // _btnWrite
        // 
        _btnWrite.Location = new Point(517, 15);
        _btnWrite.Name = "_btnWrite";
        _btnWrite.Size = new Size(70, 23);
        _btnWrite.TabIndex = 5;
        _btnWrite.Text = "쓰기";
        _btnWrite.Click += OnWrite;
        // 
        // writeHint
        // 
        writeHint.AutoSize = true;
        writeHint.Dock = DockStyle.Top;
        writeHint.ForeColor = Color.Gray;
        writeHint.Location = new Point(0, 0);
        writeHint.Name = "writeHint";
        writeHint.Padding = new Padding(12, 4, 4, 0);
        writeHint.Size = new Size(379, 19);
        writeHint.TabIndex = 1;
        writeHint.Text = "Bool: true/false 또는 1/0   |   숫자: 그대로 입력   |   String: 텍스트";
        // 
        // tabLog
        // 
        tabLog.Controls.Add(_txtLog);
        tabLog.Controls.Add(btnClearLog);
        tabLog.Location = new Point(4, 24);
        tabLog.Name = "tabLog";
        tabLog.Size = new Size(1062, 479);
        tabLog.TabIndex = 2;
        tabLog.Text = "로그";
        // 
        // _txtLog
        // 
        _txtLog.BackColor = Color.FromArgb(18, 18, 28);
        _txtLog.Dock = DockStyle.Fill;
        _txtLog.Font = new Font("Consolas", 8.5F);
        _txtLog.ForeColor = Color.FromArgb(200, 210, 220);
        _txtLog.Location = new Point(0, 24);
        _txtLog.Multiline = true;
        _txtLog.Name = "_txtLog";
        _txtLog.ReadOnly = true;
        _txtLog.ScrollBars = ScrollBars.Both;
        _txtLog.Size = new Size(1062, 455);
        _txtLog.TabIndex = 0;
        _txtLog.WordWrap = false;
        // 
        // btnClearLog
        // 
        btnClearLog.Dock = DockStyle.Top;
        btnClearLog.Location = new Point(0, 0);
        btnClearLog.Name = "btnClearLog";
        btnClearLog.Size = new Size(1062, 24);
        btnClearLog.TabIndex = 1;
        btnClearLog.Text = "로그 지우기";
        btnClearLog.Click += OnLogClearClick;
        // 
        // _statsTimer
        // 
        _statsTimer.Enabled = true;
        _statsTimer.Interval = 500;
        _statsTimer.Tick += OnStatsTick;
        // 
        // MainForm
        // 
        ClientSize = new Size(1084, 741);
        Controls.Add(root);
        Font = new Font("맑은 고딕", 9F);
        MinimumSize = new Size(900, 680);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "PlcLib TestUI";
        root.ResumeLayout(false);
        root.PerformLayout();
        grpConn.ResumeLayout(false);
        grpConn.PerformLayout();
        connInner.ResumeLayout(false);
        connInner.PerformLayout();
        rowCommon.ResumeLayout(false);
        rowCommon.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numPollMs).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numReconnMs).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numHbMs).EndInit();
        providerHost.ResumeLayout(false);
        providerHost.PerformLayout();
        _panelMcpX.ResumeLayout(false);
        _panelMcpX.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numMcpPort).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numMcpTimeout).EndInit();
        _panelS7.ResumeLayout(false);
        _panelS7.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numS7Rack).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numS7Slot).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numS7Timeout).EndInit();
        _panelModbus.ResumeLayout(false);
        _panelModbus.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numModbusPort).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusSlaveId).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusBaud).EndInit();
        ((System.ComponentModel.ISupportInitialize)_numModbusTimeout).EndInit();
        _panelMxComp.ResumeLayout(false);
        _panelMxComp.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numMxStation).EndInit();
        rowBtns.ResumeLayout(false);
        rowBtns.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        tabs.ResumeLayout(false);
        tabItems.ResumeLayout(false);
        tabItemsLayout.ResumeLayout(false);
        tabItemsLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_gridItems).EndInit();
        itemsBtnRow.ResumeLayout(false);
        itemsBtnRow.PerformLayout();
        tabWrite.ResumeLayout(false);
        tabWrite.PerformLayout();
        writePanel.ResumeLayout(false);
        writePanel.PerformLayout();
        tabLog.ResumeLayout(false);
        tabLog.PerformLayout();
        ResumeLayout(false);
    }
    private TableLayoutPanel root;
    private GroupBox grpConn;
    private TableLayoutPanel connInner;
    private FlowLayoutPanel rowCommon;
    private Label lblProvider;
    private Label lblName;
    private Label lblPollMs;
    private Label lblReconnMs;
    private Label lblHbKey;
    private Label lblHbMs;
    private Panel providerHost;
    private Label lblMcpIp;
    private Label lblMcpPort;
    private Label lblMcpFrame;
    private Label lblMcpPwd;
    private Label lblMcpTimeout;
    private Label lblS7Cpu;
    private Label lblS7Ip;
    private Label lblS7Rack;
    private Label lblS7Slot;
    private Label lblS7Timeout;
    private Label lblModbusMode;
    private Label lblModbusIp;
    private Label lblModbusPort;
    private Label lblModbusSlaveId;
    private Label lblModbusCom;
    private Label lblModbusBaud;
    private Label lblModbusTimeout;
    private Label lblMxStation;
    private Label lblMxPwd;
    private FlowLayoutPanel rowBtns;
    private Panel spacer1;
    private Panel spacer2;
    private Button btnLoad;
    private Button btnSave;
    private StatusStrip statusStrip;
    private TabControl tabs;
    private TabPage tabItems;
    private TableLayoutPanel tabItemsLayout;
    private DataGridViewTextBoxColumn colKey;
    private DataGridViewTextBoxColumn colName;
    private DataGridViewTextBoxColumn colAddress;
    private DataGridViewComboBoxColumn colType;
    private DataGridViewCheckBoxColumn colWritable;
    private DataGridViewTextBoxColumn colValue;
    private DataGridViewTextBoxColumn colQuality;
    private DataGridViewTextBoxColumn colTimestamp;
    private DataGridViewTextBoxColumn colDesc;
    private FlowLayoutPanel itemsBtnRow;
    private Button btnApply;
    private TabPage tabWrite;
    private FlowLayoutPanel writePanel;
    private Label lblWriteKey;
    private Label lblWriteVal;
    private Label writeHint;
    private TabPage tabLog;
    private Button btnClearLog;
}
