using System.Text.Json;
using System.Text.Json.Serialization;
using PlcLib;
using PlcLib.Abstractions;
using PlcLib.Common;
using PlcLib.Factories;
using PlcLib.Options;
using PlcLib.Runtime;

namespace PlcLib.TestUI;

public sealed partial class MainForm : Form
{
    // ── 비UI 상태 ─────────────────────────────────────────────
    private IPlcClient? _client;
    private PlcPollSvc? _pollSvc;
    private readonly List<PlcItemOpt> _items = new();
    private Dictionary<string, List<PlcItemOpt>> _itemsByProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _logLock = new();

    // ═════════════════════════════════════════════════════════
    public MainForm()
    {
        InitializeComponent();
        HookPlcLog();
        UpdateProviderPanel();
        UpdateButtons();
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
            Filter   = "JSON 파일|*.json|모든 파일|*.*",
            Title    = "설정 파일 열기",
            FileName = "plc-settings.json",
        };
        if (File.Exists("plc-settings.json")) dlg.InitialDirectory = Directory.GetCurrentDirectory();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var cfg  = JsonSerializer.Deserialize<PlcSettings>(json, JsonOpts);
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
            var json = JsonSerializer.Serialize(ExtractSettings(), JsonOpts);
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

    private void OnProviderChanged(object? sender, EventArgs e)
    {
        UpdateProviderPanel();
        var provider = _cboProvider.SelectedItem?.ToString() ?? "Virtual";
        _gridItems.Rows.Clear();
        if (_itemsByProvider.TryGetValue(provider, out var items))
            foreach (var item in items)
                _gridItems.Rows.Add(item.Key, item.Name, item.Address,
                    item.Type.ToString(), item.Writable, "", "", "", item.Description);
    }

    private void OnModbusModeChanged(object? sender, EventArgs e)
    {
        var tcp = _cboModbusMode.SelectedItem?.ToString() == "Tcp";
        _txtModbusIp.Enabled   = tcp;
        _numModbusPort.Enabled = tcp;
        _txtModbusCom.Enabled  = !tcp;
        _numModbusBaud.Enabled = !tcp;
    }

    private void OnLogClearClick(object? sender, EventArgs e) => _txtLog.Clear();

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
        if (cfg.PlcItemsByProvider != null)
            _itemsByProvider = new Dictionary<string, List<PlcItemOpt>>(
                cfg.PlcItemsByProvider, StringComparer.OrdinalIgnoreCase);

        var c = cfg.PlcClient;
        if (c != null)
        {
            _txtName.Text = c.Name ?? "PLC";
            var idx = _cboProvider.Items.IndexOf(c.Provider ?? "Virtual");
            _cboProvider.SelectedIndex = idx >= 0 ? idx : 0; // OnProviderChanged → 그리드 갱신

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

        UpdateProviderPanel();
    }

    private PlcSettings ExtractSettings()
    {
        SyncItemsFromGrid();
        var provider = _cboProvider.SelectedItem?.ToString() ?? "Virtual";
        _itemsByProvider[provider] = _items.ToList();
        return new PlcSettings { PlcClient = BuildClientOpt(), PlcItemsByProvider = _itemsByProvider };
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

    private static decimal Clamp(decimal v, decimal min, decimal max) =>
        v < min ? min : v > max ? max : v;

    private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true,
        Converters                  = { new JsonStringEnumConverter() },
    };
}

// ── JSON DTO ──────────────────────────────────────────────────
public sealed class PlcSettings
{
    public PlcClientOpt?                        PlcClient          { get; set; }
    public Dictionary<string, List<PlcItemOpt>>? PlcItemsByProvider { get; set; }
}
